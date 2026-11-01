using System;
using System.Collections.Generic;
using Gil.Core.Data;
using Gil.Runtime.Enemies;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>
    /// areas.json의 waves를 순서대로 소환한다. 한 웨이브를 모두 잡으면 잠시 뒤 다음 웨이브.
    /// 이미 클리어한 지역(플래그 기록)에 다시 오면 소환하지 않는다.
    /// </summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("이 웨이브가 속한 지역 씬 루트. 적의 걷기 영역을 여기서 받는다")] AreaScene _scene;
        [SerializeField, Tooltip("지역에 들어온 뒤 첫 웨이브까지(초)")] float _firstWaveDelay = 1.2f;
        [SerializeField, Tooltip("웨이브를 정리한 뒤 다음 웨이브까지(초)")] float _betweenWaves = 1.5f;

        /// <summary>적 소환 지점(자식 Transform). 씬 뷰에서 옮겨 배치한다.</summary>
        public List<Transform> SpawnPoints = new List<Transform>();
        /// <summary>소환할 수 있는 적 프리팹. 웨이브 데이터의 enemy id와 <see cref="EnemyController.EnemyId"/>로 짝짓는다.</summary>
        public List<EnemyController> EnemyPrefabs = new List<EnemyController>();

        AreaDef _area;
        readonly List<EnemyController> _alive = new List<EnemyController>();
        int _waveIndex = -1;
        float _nextWaveAt;
        bool _finished;

        public bool InProgress => !_finished && _area != null && _area.Waves.Count > 0;
        public int WaveNumber => _waveIndex + 1;
        public int WaveCount => _area?.Waves.Count ?? 0;
        public int AliveCount => _alive.Count;

        public event Action Cleared;

        public static string ClearedFlag(string areaId) => $"cleared_{areaId}";

        public void Begin(AreaDef area)
        {
            _area = area;
            _finished = area.Waves.Count == 0 || Game.Session.Flags.Contains(ClearedFlag(area.Id));
            _nextWaveAt = Time.time + _firstWaveDelay;
        }

        void Update()
        {
            if (_finished || _area == null || Game.GameplayLocked) return;
            _alive.RemoveAll(e => e == null || !e.IsAlive);
            if (_alive.Count > 0) return;

            if (_waveIndex >= 0 && _nextWaveAt < 0)
            {
                // 방금 웨이브를 정리했다.
                if (_waveIndex + 1 >= _area.Waves.Count)
                {
                    _finished = true;
                    Game.Session.Flags.Add(ClearedFlag(_area.Id));
                    Cleared?.Invoke();
                    return;
                }
                _nextWaveAt = Time.time + _betweenWaves;
            }

            if (_nextWaveAt > 0 && Time.time >= _nextWaveAt)
            {
                _nextWaveAt = -1;
                SpawnWave(++_waveIndex);
            }
        }

        void SpawnWave(int index)
        {
            var wave = _area.Waves[index];
            var spawn = UnityEngine.Random.Range(0, SpawnPoints.Count);
            foreach (var entry in wave.Entries)
            {
                var def = Game.Session.Db.Enemies[entry.EnemyId];
                var prefab = EnemyPrefabs.Find(p => p != null && p.EnemyId == entry.EnemyId);
                if (prefab == null)
                {
                    Debug.LogError($"[Gil] {name}: '{entry.EnemyId}' 적 프리팹이 연결되어 있지 않습니다");
                    continue;
                }
                for (var i = 0; i < entry.Count; i++)
                {
                    var position = SpawnPoints[spawn++ % SpawnPoints.Count].position;
                    var enemy = EnemyController.Spawn(prefab, def, position, transform, _scene.Walkable);
                    enemy.Killed += e => Game.Session.OnEnemyKilled(e.Def.Id);
                    _alive.Add(enemy);
                }
            }
            Game.Ui.Hud.ShowBanner(index == _area.Waves.Count - 1 && _area.Waves.Count > 1
                ? "경고 — 강한 적 접근"
                : $"웨이브 {index + 1} / {_area.Waves.Count}", 1.4f);
        }
    }
}

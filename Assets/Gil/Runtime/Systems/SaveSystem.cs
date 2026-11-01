using System;
using System.IO;
using Gil.Core;
using UnityEngine;

namespace Gil.Runtime.Systems
{
    /// <summary>세이브 파일 입출력. 형식 변환은 Core의 SaveData가 맡고, 여기서는 파일 위치와 예외 처리만.</summary>
    public static class SaveSystem
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "gil_save.json");

        public static bool HasSave => File.Exists(FilePath);

        public static bool Save(SaveData data)
        {
            try
            {
                // 쓰는 도중 꺼져도 기존 세이브가 깨지지 않도록 임시 파일에 쓴 뒤 교체한다.
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, data.ToJson());
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}");
                return false;
            }
        }

        public static SaveData Load()
        {
            try
            {
                return HasSave ? SaveData.FromJson(File.ReadAllText(FilePath)) : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 불러오기 실패: {e.Message}");
                return null;
            }
        }
    }
}

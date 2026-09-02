using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fishing.V2
{
    [Serializable]
    public sealed class InventoryEntryV2
    {
        public string SpeciesId;
        public int Count;
    }

    [Serializable]
    public sealed class CodexEntryV2
    {
        public string SpeciesId;
        public long FirstCaughtUtcTicks;
        public float MaxSizeCm;
        public int TotalCaught;
    }

    [Serializable]
    public sealed class SaveDataV2
    {
        public int Version = 1;
        public List<InventoryEntryV2> Inventory = new List<InventoryEntryV2>();
        public List<CodexEntryV2> Codex = new List<CodexEntryV2>();
        public int BestSessionScore;
    }

    /// <summary>
    /// 점수만 저장하지 않고 인벤토리와 도감을 원본으로 저장한다.
    /// JsonUtility가 Dictionary를 직렬화하지 못하므로 저장 포맷은 리스트를 사용한다.
    /// </summary>
    public sealed class PlayerDataV2
    {
        private const string FileName = "fishing_v2_save.json";
        private static PlayerDataV2 _instance;

        public static PlayerDataV2 Instance
        {
            get
            {
                if (_instance == null) _instance = Load();
                return _instance;
            }
        }

        public SaveDataV2 Data { get; private set; }

        private PlayerDataV2(SaveDataV2 data)
        {
            Data = data ?? new SaveDataV2();
        }

        public void RecordCatch(FishSpeciesConfig species, float sizeCm)
        {
            if (species == null) return;

            InventoryEntryV2 inventory = Data.Inventory.Find(entry => entry.SpeciesId == species.SpeciesId);
            if (inventory == null)
            {
                inventory = new InventoryEntryV2 { SpeciesId = species.SpeciesId, Count = 0 };
                Data.Inventory.Add(inventory);
            }
            inventory.Count++;

            CodexEntryV2 codex = Data.Codex.Find(entry => entry.SpeciesId == species.SpeciesId);
            if (codex == null)
            {
                codex = new CodexEntryV2
                {
                    SpeciesId = species.SpeciesId,
                    FirstCaughtUtcTicks = DateTime.UtcNow.Ticks,
                    MaxSizeCm = sizeCm,
                    TotalCaught = 0
                };
                Data.Codex.Add(codex);
            }
            codex.MaxSizeCm = Mathf.Max(codex.MaxSizeCm, sizeCm);
            codex.TotalCaught++;
        }

        public int InventoryCount(string speciesId)
        {
            InventoryEntryV2 entry = Data.Inventory.Find(item => item.SpeciesId == speciesId);
            return entry != null ? entry.Count : 0;
        }

        public bool HasCodex(string speciesId)
        {
            return Data.Codex.Exists(entry => entry.SpeciesId == speciesId);
        }

        public void RegisterSessionScore(int score)
        {
            Data.BestSessionScore = Mathf.Max(Data.BestSessionScore, score);
        }

        public void Save()
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(Data, true));
        }

        private static PlayerDataV2 Load()
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            if (!File.Exists(path)) return new PlayerDataV2(new SaveDataV2());

            try
            {
                SaveDataV2 data = JsonUtility.FromJson<SaveDataV2>(File.ReadAllText(path));
                return new PlayerDataV2(data ?? new SaveDataV2());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Fishing V2 save load failed; starting with empty data. " + exception.Message);
                return new PlayerDataV2(new SaveDataV2());
            }
        }
    }
}

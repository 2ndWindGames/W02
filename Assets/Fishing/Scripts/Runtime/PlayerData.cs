using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 저장 원본. 기획서 §8.
    ///
    /// ⚠️ 점수를 저장하지 않는다. 인벤토리로 점수를 계산할 수는 있지만
    ///    점수로 인벤토리를 계산할 수는 없다. 정보가 더 많은 쪽을 저장한다.
    ///    나중에 납품·강화가 붙을 때 이 구조 덕분에 세이브 마이그레이션이 필요 없다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public List<InventoryEntry> inventory = new List<InventoryEntry>();
        public List<CodexEntry> codex = new List<CodexEntry>();
        public int bestSessionScore;

        [Serializable] public class InventoryEntry { public string speciesId; public int count; }
        [Serializable] public class CodexEntry
        {
            public string speciesId;
            public long  firstCaughtUtc;
            public float maxSizeCm;
            public int   totalCaught;
        }
    }

    public class PlayerData
    {
        static PlayerData _instance;
        public static PlayerData Instance
        {
            get { if (_instance == null) _instance = Load(); return _instance; }
        }

        public SaveData Data { get; private set; } = new SaveData();

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, "fishing_save.json");

        static PlayerData Load()
        {
            var pd = new PlayerData();
            try
            {
                if (File.Exists(Path))
                {
                    string json = File.ReadAllText(Path);
                    pd.Data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Fishing] 세이브 로드 실패: {e.Message}"); }
            return pd;
        }

        public void Save()
        {
            try { File.WriteAllText(Path, JsonUtility.ToJson(Data)); }
            catch (Exception e) { Debug.LogWarning($"[Fishing] 세이브 저장 실패: {e.Message}"); }
        }

        /// <summary>획득 기록. 인벤토리와 도감 둘 다 갱신한다.</summary>
        public void RecordCatch(FishSpeciesSO sp, float sizeCm)
        {
            var inv = Data.inventory.Find(e => e.speciesId == sp.speciesId);
            if (inv == null) Data.inventory.Add(new SaveData.InventoryEntry { speciesId = sp.speciesId, count = 1 });
            else inv.count++;

            var cx = Data.codex.Find(e => e.speciesId == sp.speciesId);
            if (cx == null)
            {
                Data.codex.Add(new SaveData.CodexEntry
                {
                    speciesId = sp.speciesId,
                    firstCaughtUtc = DateTime.UtcNow.Ticks,
                    maxSizeCm = sizeCm,
                    totalCaught = 1,
                });
            }
            else
            {
                cx.totalCaught++;
                if (sizeCm > cx.maxSizeCm) cx.maxSizeCm = sizeCm;
            }
        }

        // ── 파생값 — 저장하지 않는다 ─────────────────────────────
        public int InventoryCount(string speciesId)
            => Data.inventory.Find(e => e.speciesId == speciesId)?.count ?? 0;

        public int ComputeTotalValue(IReadOnlyList<FishSpeciesSO> allSpecies)
        {
            int total = 0;
            foreach (var sp in allSpecies) total += InventoryCount(sp.speciesId) * sp.score;
            return total;
        }

        public bool HasCodex(string speciesId) => Data.codex.Exists(e => e.speciesId == speciesId);
    }
}

using System.Numerics;
using BeastNav.Models;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace BeastNav.Services;

public static class BeastmasterFieldSource
{
    private const float MapSpan = 41f;
    private const float RawSpan = 2048f;
    private const float RawCenter = RawSpan / 2f;

    private static readonly FieldEntry[] Entries =
    [
        new(2, 4, 23.400f, 16.300f, 1, 2, "Central Shroud", ["スクウィレル"]),
        new(3, 15, 23.725f, 25.375f, 3, 4, "Middle La Noscea", ["シープ"]),
        new(4, 15, 21.047f, 19.072f, 4, 6, "Middle La Noscea", ["プギル"]),
        new(5, 7, 27.860f, 24.313f, 5, 9, "North Shroud", ["オポオポ"]),
        new(6, 16, 31.049f, 17.504f, 4, 9, "Lower La Noscea", ["ドードー"]),
        new(7, 20, 20.303f, 28.133f, 6, 8, "Western Thanalan", ["ラスティコブラン"]),
        new(8, 4, 21.300f, 27.200f, 10, 12, "Central Shroud", ["ダイアマイト"]),
        new(9, 15, 15.339f, 14.639f, 10, 13, "Middle La Noscea", ["メガロクラブ"]),
        new(10, 21, 22.807f, 28.832f, 1, 4, "Central Thanalan", ["ヒュージ・ホーネット"]),
        new(11, 20, 16.314f, 16.300f, 13, 13, "Western Thanalan", ["バザード"]),
        new(12, 15, 21.550f, 17.112f, 5, 7, "Middle La Noscea", ["タイニー・マンドラゴラ"]),
        new(13, 4, 19.100f, 27.400f, 14, 14, "Central Shroud", ["ゲシュンペスト"]),
        new(14, 15, 20.378f, 19.576f, 4, 8, "Middle La Noscea", ["プーク・ハッチリング"]),
        new(15, 20, 15.938f, 16.487f, 13, 13, "Western Thanalan", ["シックシェル"]),
        new(16, 18, 22.475f, 22.250f, 16, 16, "Western La Noscea", ["キラーマンティス"]),
        new(19, 16, 26.767f, 15.600f, 7, 7, "Lower La Noscea", ["ケーブバット"]),
        new(20, 4, 22.700f, 24.900f, 10, 10, "Central Shroud", ["ローズレット"]),
        new(21, 18, 24.500f, 23.600f, 16, 16, "Western La Noscea", ["ロズリトペリカン"]),
        new(22, 20, 27.200f, 24.787f, 3, 4, "Western Thanalan", ["カクター"]),
        new(23, 23, 23.156f, 12.533f, 29, 29, "Southern Thanalan", ["サンドストーン・ゴーレム"]),
        new(24, 17, 29.162f, 35.713f, 30, 30, "Eastern La Noscea", ["アプカル"]),
        new(25, 21, 21.171f, 23.882f, 12, 17, "Central Thanalan", ["ジャイアントトータス"]),
        new(26, 15, 18.775f, 17.225f, 8, 8, "Middle La Noscea", ["ウーンデッド・オーロックス"]),
        new(27, 20, 16.930f, 14.580f, 14, 14, "Western Thanalan", ["スカフィテ"]),
        new(28, 23, 18.656f, 34.859f, 31, 32, "Southern Thanalan", ["サンドウォーム"]),
        new(29, 21, 17.480f, 23.500f, 7, 7, "Central Thanalan", ["スプリガン・グレイブラバー"]),
        new(30, 16, 27.900f, 20.638f, 12, 17, "Lower La Noscea", ["モスレスグゥーブー"]),
        new(31, 16, 24.500f, 22.500f, 4, 4, "Lower La Noscea", ["リバートード"]),
        new(32, 17, 30.795f, 24.895f, 33, 33, "Eastern La Noscea", ["コリブリ"]),
        new(33, 30, 14.400f, 15.100f, 34, 34, "Outer La Noscea", ["クァール", "チョッパー"]),
        new(34, 4, 30.900f, 20.300f, 9, 9, "Central Shroud", ["アノール"]),
        new(35, 23, 24.808f, 38.342f, 32, 32, "Southern Thanalan", ["ドレイク"]),
        new(36, 4, 22.900f, 17.100f, 12, 12, "Central Shroud", ["トレント・サップリング"]),
        new(39, 4, 14.000f, 21.900f, 31, 31, "Central Shroud", ["ストローパー"]),
        new(40, 15, 20.471f, 18.871f, 7, 7, "Middle La Noscea", ["ボギー"]),
        new(41, 4, 26.500f, 18.000f, 6, 6, "Central Shroud", ["ブラックエフト"]),
        new(42, 25, 26.500f, 12.956f, 45, 45, "Mor Dhona", ["レイクコブラ"]),
    ];

    private static readonly DetailEntry[] Details =
    [
        new(1, "Job quest", []),
        new(17, "Copperbell Mines", ["イコラウス・アイル"]),
        new(18, "Halatali", ["ドクトル"]),
        new(37, "Cutter's Cry", ["ミュルミドン・ソルジャー", "ミュルミドン・セントリー", "ミュルミドン・ガード", "ミュルミドン・マーシャル", "ミュルミドン・プリンセス"]),
        new(38, "Cutter's Cry / A Relic Reborn: The Chimera", ["キマイラ", "ドルムキマイラ"]),
        new(43, "A Relic Reborn: The Hydra", ["ハイドラ"]),
        new(44, "The Lost City of Amdapor", ["ガッドフライ"]),
        new(45, "The Lost City of Amdapor", ["ロッティング・グルマン"]),
        new(46, "Pharos Sirius / The Black Bird FATE (Sea of Clouds)", ["ズー", "ズー・クックル", "マザーアンズー", "アンズー・プレット"]),
        new(47, "Snowcloak / Ice Commander FATE (Coerthas Western Highlands)", ["ワンディル", "アイスコマンダー"]),
        new(48, "Sastasha (Hard)", ["カーラボス"]),
        new(49, "The Second Coil of Bahamut - Turn 1", ["ラフレシア"]),
        new(50, "Labyrinth of the Ancients", ["キングベヒーモス"]),
    ];

    public static IReadOnlyList<BeastDestination> CreateDestinations(IDataManager dataManager, IReadOnlyList<BeastPet> pets)
    {
        var mapSheet = dataManager.GetExcelSheet<Map>();
        if (mapSheet is null)
        {
            return [];
        }

        var petByXbmRow = pets.ToDictionary(static pet => pet.XbmRowId, static pet => pet);
        var results = new List<BeastDestination>();

        foreach (var entry in Entries)
        {
            if (!petByXbmRow.TryGetValue(entry.XbmRowId, out var pet))
            {
                continue;
            }

            var map = mapSheet.FirstOrDefault(row => row.RowId == entry.MapRowId);
            if (map.RowId == 0)
            {
                continue;
            }

            var position = MapToWorld(entry.MapX, entry.MapY, map.SizeFactor, map.OffsetX, map.OffsetY);

            results.Add(new BeastDestination
            {
                XbmRowId = entry.XbmRowId,
                PetRowId = pet.PetRowId,
                TerritoryId = map.TerritoryType.RowId,
                MapId = entry.MapRowId,
                MapX = entry.MapX,
                MapY = entry.MapY,
                X = position.X,
                Y = position.Y,
                Z = position.Z,
                Radius = 7.5f,
                EnemyLevelMin = entry.EnemyLevelMin,
                EnemyLevelMax = entry.EnemyLevelMax,
                TargetNames = entry.TargetNames.ToList(),
                Label = $"{entry.Label} X{MapCoord(entry.MapX)} Y{MapCoord(entry.MapY)}",
                Source = "built-in",
                Approximate = true,
            });
        }

        var knownRows = results.Select(static destination => destination.XbmRowId).ToHashSet();
        foreach (var detail in Details)
        {
            if (!knownRows.Add(detail.XbmRowId) || !petByXbmRow.ContainsKey(detail.XbmRowId))
            {
                continue;
            }

            var pet = petByXbmRow[detail.XbmRowId];
            results.Add(new BeastDestination
            {
                XbmRowId = detail.XbmRowId,
                PetRowId = pet.PetRowId,
                Label = detail.Label,
                TargetNames = detail.TargetNames.ToList(),
                Source = "built-in",
                Approximate = true,
            });
        }

        return results;
    }

    public static Vector3 MapToWorld(float mapX, float mapY, uint scale, int offsetX, int offsetY)
    {
        var scaleFactor = scale / 100f;
        var rawX = ((mapX - 1f) / MapSpan * RawSpan) - RawCenter;
        var rawZ = ((mapY - 1f) / MapSpan * RawSpan) - RawCenter;
        return new Vector3((rawX / scaleFactor) - offsetX, 0f, (rawZ / scaleFactor) - offsetY);
    }

    private static string MapCoord(float value)
        => value.ToString("0.0");

    private sealed record FieldEntry(
        uint XbmRowId,
        uint MapRowId,
        float MapX,
        float MapY,
        ushort EnemyLevelMin,
        ushort EnemyLevelMax,
        string Label,
        string[] TargetNames);

    private sealed record DetailEntry(uint XbmRowId, string Label, string[] TargetNames);
}

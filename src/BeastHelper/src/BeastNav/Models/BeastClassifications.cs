namespace BeastNav.Models;

public static class BeastClassifications
{
    // Compatibility data for the original 50 beasts, guarded by row/pet/icon
    // identity. Prefer the live generated XBMPet.Classification when available.
    // https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en/XBMPet.csv
    // Classification semantics: https://raw.githubusercontent.com/xivdev/EXDSchema/latest/XBMPet.yml
    private static readonly byte[] Reference =
    [0, 1,1,1,5,1,3,7,2,5,2,3,4,8,6,5,2,8,7,3,4,6,4,7,3,6,
        1,5,2,7,1,5,3,1,6,6,4,2,1,4,8,5,6,6,2,8,3,7,5,4,1];

    public static byte ForReferenceRow(uint row, uint pet, uint icon)
        => row is >= 1 and <= 50 && icon == 242000 + row
            && pet == row + (row <= 4 ? 46u : 47u) ? Reference[row] : (byte)0;

    public static string Name(byte classification) => classification switch
    {
        1 => "Beastkin", 2 => "Vilekin", 3 => "Cloudkin", 4 => "Seedkin",
        5 => "Wavekin", 6 => "Scalekin", 7 => "Soulkin", 8 => "Ashkin",
        _ => "Unknown",
    };
}

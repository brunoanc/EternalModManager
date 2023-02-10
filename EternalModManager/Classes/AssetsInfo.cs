using System.Collections.Generic;

namespace EternalModManager.Classes;

// AssetsInfo JSON class (for deserialization)
public class AssetsInfo
{
    public List<AssetsInfoLayer>? Layers { get; set; }
    public List<AssetsInfoMap>? Maps { get; set; }
    public List<AssetsInfoResource>? Resources { get; set; }
    public List<AssetsInfoAsset>? Assets { get; set; }
}

public class AssetsInfoLayer
{
    public string? Name { get; set; }
}
public class AssetsInfoMap
{
    public string? Name { get; set; }
}

public class AssetsInfoResource
{
    public string? Name { get; set; }
    public bool? Remove { get; set; }
    public bool? PlaceFirst { get; set; }
    public bool? PlaceBefore { get; set; }
    public string? PlaceByName { get; set; }
}

public class AssetsInfoAsset
{
    public ulong? StreamDbHash { get; set; }
    public string? ResourceType { get; set; }
    public byte? Version { get; set; }
    public string? Name { get; set; }
    public string? MapResourceType { get; set; }
    public bool? Remove { get; set; }
    public bool? PlaceBefore { get; set; }
    public string? PlaceByName { get; set; }
    public string? PlaceByType { get; set; }
    public byte? SpecialByte1 { get; set; }
    public byte? SpecialByte2 { get; set; }
    public byte? SpecialByte3 { get; set; }
}

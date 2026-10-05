namespace MegaDeck;

// Los nombres de las propiedades se mantienen para seguir leyendo los config.json existentes.
public class AppConfig
{
    public string RomsDirectory_SegaCD { get; set; } = "";
    public string RomsDirectory_Saturn { get; set; } = "";
    public string RomsDirectory_PSX { get; set; } = "";
    public string RomsDirectory_PCFX { get; set; } = "";
    public string RomsDirectory_PCECD { get; set; } = "";

    public string GetRomsDirectory(string systemId) => systemId switch
    {
        "segacd" => RomsDirectory_SegaCD,
        "saturn" => RomsDirectory_Saturn,
        "psx" => RomsDirectory_PSX,
        "pcfx" => RomsDirectory_PCFX,
        "pcecd" => RomsDirectory_PCECD,
        _ => ""
    };

    public void SetRomsDirectory(string systemId, string path)
    {
        switch (systemId)
        {
            case "segacd": RomsDirectory_SegaCD = path; break;
            case "saturn": RomsDirectory_Saturn = path; break;
            case "psx": RomsDirectory_PSX = path; break;
            case "pcfx": RomsDirectory_PCFX = path; break;
            case "pcecd": RomsDirectory_PCECD = path; break;
        }
    }
}

namespace FuturesTicker.Services;

/// <summary>
/// A handy reference list of common domestic commodity futures
/// continuous contracts (主力连续, suffixed with 0).
/// </summary>
public static class CommonContracts
{
    public static readonly IReadOnlyList<(string Code, string Name)> Items = new List<(string, string)>
    {
        ("RB0", "螺纹钢"), ("HC0", "热卷"), ("I0", "铁矿石"), ("J0", "焦炭"), ("JM0", "焦煤"),
        ("CU0", "沪铜"), ("AL0", "沪铝"), ("ZN0", "沪锌"), ("NI0", "沪镍"), ("SS0", "不锈钢"),
        ("AU0", "沪金"), ("AG0", "沪银"), ("SC0", "原油"), ("FU0", "燃油"), ("LU0", "低硫燃油"),
        ("TA0", "PTA"), ("MA0", "甲醇"), ("EG0", "乙二醇"), ("PP0", "聚丙烯"), ("V0", "PVC"),
        ("SA0", "纯碱"), ("FG0", "玻璃"), ("C0", "玉米"), ("CS0", "玉米淀粉"),
        ("M0", "豆粕"), ("Y0", "豆油"), ("P0", "棕榈油"), ("SR0", "白糖"), ("CF0", "棉花"),
        ("RU0", "橡胶"), ("BU0", "沥青"), ("SP0", "纸浆"), ("PG0", "LPG"), ("OI0", "菜油"),
    };
}

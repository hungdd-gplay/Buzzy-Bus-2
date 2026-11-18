/// <summary>
/// Item thu thập của Challenge. Logic chung (spawn, nhặt, bay về khung, x2...) ở CollectItemBase.
/// Item challenge riêng: kế thừa class này và override các hàm *Handle.
/// </summary>
public class ItemChallenge : CollectItemBase
{
    public ChallengeType typeChallenge;

    /// <summary>Mặc định cũ: mọi challenge được x2 khi có X2WeeklyChallgeneItem.</summary>
    protected override GiftType GetTypeItemBoostX2()
    {
        var _typeBoost = base.GetTypeItemBoostX2();
        return _typeBoost != GiftType.None ? _typeBoost : GiftType.X2WeeklyChallgeneItem;
    }
}

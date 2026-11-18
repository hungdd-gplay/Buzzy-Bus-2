using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông số các gói IAP (hiển thị, giảm giá, giới hạn mua, điều kiện xuất hiện).
/// Lịch xuất hiện theo segment: SaleSegemntData.packSchedules. Quản lý: Tools > Base Project > IAP > Pack Manager.
/// </summary>
[CreateAssetMenu(menuName = "ScriptableObject/SalePackDatabase", fileName = "SalePackDatabase.asset")]
public class SalePackDatabase : ScriptableObject
{
    public List<SalePackDataModel> packs = new List<SalePackDataModel>();

    public SalePackDataModel GetPack(TypePackIAP typePackIAP)
    {
        if (packs == null)
            return null;
        for (int i = 0; i < packs.Count; i++)
            if (packs[i] != null && packs[i].typePackIAP == typePackIAP)
                return packs[i];
        return null;
    }

    public bool IsHasPack(TypePackIAP typePackIAP)
    {
        return GetPack(typePackIAP) != null;
    }

    /// <summary>Gói thuộc nhóm exclusive.</summary>
    public List<SalePackDataModel> GetPacksInGroup(string groupExclusive)
    {
        var _result = new List<SalePackDataModel>();
        if (string.IsNullOrEmpty(groupExclusive) || packs == null)
            return _result;
        foreach (var _pack in packs)
            if (_pack != null && _pack.groupExclusive == groupExclusive)
                _result.Add(_pack);
        return _result;
    }
}

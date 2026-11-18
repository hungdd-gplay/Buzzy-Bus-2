using UnityEngine;

/// <summary>
/// Loại item hiển thị (giữ nguyên giá trị số của Configs.ITEM_TYPE cũ để các file .asset không bị lệch dữ liệu).
/// </summary>
public enum ItemDataType
{
    ITEM_1 = 1,
    ITEM_2 = 2,
    ITEM_3 = 3,
    ITEM_4 = 4,
    ITEM_5 = 5,
    ITEM_6 = 6,
    ITEM_7 = 7,
    ITEM_8 = 8,
    ITEM_9 = 9,
    ITEM_10 = 10,
    ITEM_11 = 11,
    ITEM_12 = 12,
    ITEM_13 = 13,
    ITEM_14 = 14,
    ITEM_15 = 15,
    ITEM_16 = 16,
    ITEM_17 = 17,
    ITEM_18 = 18,
    ITEM_19 = 19,
    ITEM_20 = 20,
    ITEM_EGG = 21,
    ITEM_ROYAL = 22,
    ITEM_COIN = 23,
    ITEM_DIAMOND = 24
}

[CreateAssetMenu(fileName = "ItemData", menuName = "ScriptableObjects/ItemData", order = 1)]
public class ItemData : ScriptableObject
{
    public ItemDataType itemType;
}

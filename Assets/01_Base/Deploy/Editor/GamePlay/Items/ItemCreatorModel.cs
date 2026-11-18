using System;
using UnityEngine;

namespace BaseProject.EditorTools.Item
{
    /// <summary>Thông tin tạo 1 item mới (Model). Lưu EditorPrefs dạng JSON (trừ icon).</summary>
    [Serializable]
    public class ItemCreateSetting
    {
        [Tooltip("Tên item dạng PascalCase, ví dụ: Bomb, AddTime, Shuffle.")]
        public string nameItem = "NewItem";
        [Tooltip("Mô tả ngắn, ghi vào script và GiftDatabase.")]
        public string description = "";
        public ItemBase.GetPlace getPlace = ItemBase.GetPlace.GamePlayUI;
        public string folderScript = ProjectPaths.GAMEPLAY_DEPLOY + "/Controller/Items";

        [Header("Segment")]
        public int unlockLevel = 1;
        public int price = 100;
        public int packAmount = 3;
        public int amountDefault = 2;
        public float durationActive;
        public float cooldown;
        public int maxUsePerLevel;
        public bool isConsumeOnActive = true;
        public string workValueText = "";

        [Header("Tự động")]
        public bool isAddToSegment = true;
        public bool isAddToGiftDatabase = true;
        public bool isAddToScene = true;

        public string NameEnum => nameItem + "Item";
        public string NameClass => "Item" + nameItem;
    }

    /// <summary>Việc còn phải làm sau khi Unity biên dịch enum + script mới (lưu SessionState).</summary>
    [Serializable]
    public class ItemPendingCreate
    {
        public ItemCreateSetting setting;
        public int valueEnum;
        public string pathIcon;
    }
}

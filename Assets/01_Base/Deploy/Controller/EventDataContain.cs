using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data của các sự kiện ngoài lề (Halloween, Noel, mini game, challenge...).
/// Đặt trên GameObject con "EventData" của DataContain.
/// Mỗi sự kiện = 1 ScriptableObject, đăng ký theo eventKey để tra cứu.
/// </summary>
public class EventDataContain : MonoBehaviour
{
    [Serializable]
    public class EventDataEntry
    {
        [Tooltip("Mã sự kiện (vd: halloween_2026). Dùng chung với UserEventProfile.")]
        public string eventKey;
        public bool isEnable = true;
        public ScriptableObject data;
    }

    public List<EventDataEntry> lstEventData = new List<EventDataEntry>();

    public ScriptableObject GetEventData(string eventKey)
    {
        for (int i = 0; i < lstEventData.Count; i++)
        {
            var _entry = lstEventData[i];
            if (_entry != null && _entry.isEnable && _entry.eventKey == eventKey)
                return _entry.data;
        }
        return null;
    }

    public T GetEventData<T>(string eventKey) where T : ScriptableObject
    {
        return GetEventData(eventKey) as T;
    }

    public T GetEventData<T>() where T : ScriptableObject
    {
        for (int i = 0; i < lstEventData.Count; i++)
        {
            var _entry = lstEventData[i];
            if (_entry != null && _entry.isEnable && _entry.data is T _data)
                return _data;
        }
        return null;
    }

    public bool IsEventEnable(string eventKey)
    {
        return GetEventData(eventKey) != null;
    }
}

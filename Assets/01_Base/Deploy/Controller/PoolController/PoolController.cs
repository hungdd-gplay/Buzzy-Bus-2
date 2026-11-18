using UnityEngine;

/// <summary>
/// Quản lý các pool object dùng chung. Pool riêng của gameplay cũ (PoolBirds) đã bị gỡ,
/// gameplay mới đăng ký pool của mình trong InitPoolGameplay().
/// </summary>
public class PoolController : MonoBehaviour
{
    public bool isLoadPoolDone;

    public void Init()
    {
        isLoadPoolDone = false;
        InitPoolGameplay();
    }

    /// <summary>Khởi tạo pool cho gameplay mới (để trống).</summary>
    protected virtual void InitPoolGameplay()
    {
        isLoadPoolDone = true;
    }
}

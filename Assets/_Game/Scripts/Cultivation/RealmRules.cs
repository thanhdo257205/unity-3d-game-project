using UnityEngine;

/// <summary>
/// Luật chênh lệch cảnh giới (Phần 5.3). Cùng đại cảnh giới thì sát thương bình thường.
/// Bên mạnh hơn một đại cảnh giới gây sát thương x2.5, bên yếu hơn gây sát thương x0.4 (nhân dồn theo từng bậc chênh).
/// Hai hệ số chỉnh trong Inspector của PlayerStats (nhóm "Chênh lệch cảnh giới").
/// </summary>
public static class RealmRules
{
    /// <summary>Hệ số sát thương mỗi bậc đại cảnh giới khi người đánh mạnh hơn người bị đánh.</summary>
    public static float TakenPerGap = 2.5f;

    /// <summary>Hệ số sát thương mỗi bậc đại cảnh giới khi người đánh yếu hơn người bị đánh.</summary>
    public static float DealtPerGap = 0.4f;

    /// <summary>Hệ số nhân sát thương khi bên có đại cảnh giới attackerMajor đánh bên có defenderMajor.</summary>
    public static float DamageMultiplier(int attackerMajor, int defenderMajor)
    {
        int gap = attackerMajor - defenderMajor;
        if (gap == 0) return 1f;
        return gap > 0 ? Mathf.Pow(TakenPerGap, gap) : Mathf.Pow(DealtPerGap, -gap);
    }
}

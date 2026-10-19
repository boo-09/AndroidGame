# 宝箱のランダムスポーン：担当部分の抜粋

既存のTreasureChest.csに追加したランダム配置部分だけを掲載しています。既存の開封処理・スキル選択の呼び出し・宝箱を非表示にする処理は含めていません。

- Inspectorで候補地点をcandidatePointsに設定します。
- Awake時に、有効な候補地点から1か所をランダムに選びます。nullの候補は抽選から除外します。
- 候補が未設定、またはすべてnullの場合は元の位置を保ちます。
- 配置先の位置だけを反映し、宝箱の正面の向きは保ちます。
- SelectedIndexには、選ばれた候補の配列インデックスを記録します。未選択時は-1です。

以下は担当部分をクラス形式で示した資料です。公開用リポジトリ内のコンパイル対象にはしていません。

```csharp
using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    [Header("ランダム配置（候補なしの場合は固定位置）")]
    public Transform[] candidatePoints = new Transform[0];

    public int SelectedIndex { get; private set; } = -1;

    private void Awake()
    {
        if (candidatePoints == null)
            return;

        int validCount = 0;
        foreach (Transform point in candidatePoints)
        {
            if (point != null)
                validCount++;
        }

        if (validCount == 0)
            return;

        int selection = Random.Range(0, validCount);
        for (int i = 0; i < candidatePoints.Length; i++)
        {
            Transform point = candidatePoints[i];
            if (point == null || selection-- != 0)
                continue;

            SelectedIndex = i;
            // 候補地点の回転は使わず、宝箱に設定した正面向きを保つ。
            transform.position = point.position;
            return;
        }
    }
}
```

# ミニマップ探索範囲拡大：担当部分の抜粋

PlayerSkills.csの全体は掲載せず、担当したメソッドだけを抜粋しています。単体のコンパイル用コードではありません。

スキル番号9の取得時にGainMapRevealUp()を呼び出し、探索半径を1増やします。スキル選択中にミニマップが非表示でも検索できるよう、非アクティブなオブジェクトを含めています。元クラスのminimapUI（GameObject）を参照します。ステージ移行後の復元処理からも、未適用の取得回数分を呼び出します。

```csharp
    private void GainMapRevealUp()
    {
        MinimapController minimap = FindMinimap();

        if (minimap != null)
        {
            minimap.AddRevealRadius(1f);

            Debug.Log(
                "マップ探索範囲 +1"
            );
        }
        else
        {
            Debug.LogWarning(
                "MinimapControllerが見つからないため、マップ探索範囲を拡大できません。"
            );
        }
    }


    // =========================================================
    // MinimapController検索
    // =========================================================

    private MinimapController FindMinimap()
    {
        // The minimap is hidden while the player chooses a skill.
        if (minimapUI != null)
        {
            MinimapController minimap =
                minimapUI.GetComponentInChildren<MinimapController>(true);

            if (minimap != null)
            {
                return minimap;
            }
        }

        foreach (
            MinimapController minimap
            in FindObjectsByType<MinimapController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            )
        )
        {
            if (minimap.gameObject.scene == gameObject.scene)
            {
                return minimap;
            }
        }

        return null;
    }
```

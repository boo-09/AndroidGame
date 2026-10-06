# AndroidGame — 担当実装のポートフォリオ

Unityで共同制作したAndroid向けゲームのうち、私が担当した実装をまとめています。ゲーム全体ではなく、コードを閲覧するためのリポジトリです。

## 担当範囲

| 分野 | 内容 | コード |
| --- | --- | --- |
| ミニマップ | マップ・プレイヤー位置・未探索領域の表示、探索領域の更新、画面サイズへの対応 | [MinimapController.cs](Assets/Scripts/MinimapController.cs) |
| ステータス | 階層・攻撃力・移動速度・討伐数の表示とセーフエリア対応 | [CharacterStatusHUD.cs](Assets/Scripts/CharacterStatusHUD.cs) |
| ステータス引き継ぎ | ステージ間の保存、クリア・ゲームオーバー時の保存とリセット | [PlayerStatusCarrier.cs](Assets/Scripts/PlayerStatusCarrier.cs)、[PanelStatusTrigger.cs](Assets/Scripts/PanelStatusTrigger.cs) |
| メニュー | ポーズ、再開、ロビーへの移動、UIの生成と配置 | [PauseMenuController.cs](Assets/Scripts/PauseMenuController.cs) |
| ゲーム進行・UI連携 | カウントダウン、タイム・鍵の表示、クリア・ゲームオーバー、各UIの初期化 | [GameManager.cs](Assets/Scripts/GameManager.cs) |
| プレイヤー | プレイヤー実装、移動・攻撃・HP・弾数・リロードの制御、攻撃モーションとの連携 | [PlayerController2.cs](Assets/Scripts/PlayerController2.cs) |
| スキル | マップの視野拡大に関わる部分のみ | [担当部分の抜粋](docs/MapRevealSkill.md) |
| UI配置の補助 | 入れ子のCanvasのレイアウト調整 | [NestedCanvasLayout.cs](Assets/Scripts/NestedCanvasLayout.cs) |

## 実装のポイント

- ミニマップはRenderTextureと探索状態のテクスチャを使用し、移動に応じて探索領域を更新します。
- マップの視野拡大スキルは、ミニマップの探索半径を1広げます。スキルシステム全体は掲載していません。
- 攻撃モーションはAnimatorのDoAttackトリガーで呼び出します。カプセルによる攻撃判定、同じ敵への重複ダメージの防止、後方・障害物越しの攻撃の除外を実装しています。

## 開発環境と依存関係

元プロジェクトのUnityバージョン：**6000.3.14f1**。Unity UI、TextMesh Pro、Input Systemを使用しています。

このリポジトリ単体ではゲームを起動できません。シーン、Prefab、モデル、アニメーション素材、画像、フォント、パッケージ設定は含めていません。攻撃モーションについては、コード側の連携を掲載しています。

掲載コードは元プロジェクトからコピーし、改行と行末の空白のみ整えたものです。閲覧時は次の未収録の依存関係を前提としてください。

- StarterAssetsInputs：入力処理。
- CameraController：ステータス引き継ぎ時のカメラ設定。
- Enemy：敵へのダメージとノックバック。
- PlayerSkills：スキル取得と討伐時の通知。担当部分のみMarkdownに抜粋。
- Inspectorで設定する参照、AnimatorとDoAttackトリガー、ステージおよびロビーのシーン。

共同制作のゲーム全体や外部素材を再配布するリポジトリではありません。

# AndroidGame — 担当実装のポートフォリオ

Unityで共同制作したAndroid向けゲームのうち、私が担当した実装をまとめています。ゲーム全体ではなく、コードを閲覧するためのリポジトリです。
## プロジェクト概要

元プロジェクトはPrivate環境で3人による共同開発を行っており、本リポジトリは採用選考向けに自身の担当実装を抜粋したものです。

本リポジトリでは、共同制作物全体ではなく、
私自身が担当・実装したコードを中心に掲載しています。

- チーム人数：3人
- 開発環境：Unity 6000.3.14f1 / C#
- 開発状況：現在開発中
- 担当：プレイヤー、UI、ミニマップ、ゲーム進行、Stage6〜10の追加実装、宝箱のランダム配置、サウンド制御

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
| Stage6〜10の仕掛け | 順序入力・複数スイッチによる謎解き、鍵とゲートの解放、スイッチ接触判定 | [FortPuzzle.cs](Assets/Scripts/FortPuzzle.cs)、[FortPuzzleSwitch.cs](Assets/Scripts/FortPuzzleSwitch.cs) |
| Stage6〜10の戦闘・罠 | 予告付きの床罠、投射物の移動と衝突判定、毒・継続回復 | [FortTrap.cs](Assets/Scripts/FortTrap.cs)、[FortThrownProjectile.cs](Assets/Scripts/FortThrownProjectile.cs)、[PlayerHealthEffects.cs](Assets/Scripts/PlayerHealthEffects.cs) |
| ステージ制作ツール | Stage6〜10の生成・配置と設定の検証、Play Modeでの進行・戦闘チェック | [FortStageBuilder.cs](Assets/Scripts/Editor/FortStageBuilder.cs)、[FortStageValidator.cs](Assets/Scripts/Editor/FortStageValidator.cs)、[FortStagePlayChecks.cs](Assets/Scripts/Editor/FortStagePlayChecks.cs)、[FortCombatPlayChecks.cs](Assets/Scripts/Editor/FortCombatPlayChecks.cs) |
| 宝箱 | 既存の宝箱スクリプトに追加した、候補地点からのランダムスポーン | [担当部分の抜粋](docs/TreasureChestRandomSpawn.md) |
| サウンド | ステージBGM、クリア・ゲームオーバー・撃破の効果音、攻撃モーションのAnimation Eventからの再生 | [GameAudio.cs](Assets/Scripts/GameAudio.cs) |

## 実装のポイント

- ミニマップはRenderTextureと探索状態のテクスチャを使用し、移動に応じて探索領域を更新します。
- マップの視野拡大スキルは、ミニマップの探索半径を1広げます。スキルシステム全体は掲載していません。
- 攻撃モーションはAnimatorのDoAttackトリガーで呼び出します。カプセルによる攻撃判定、同じ敵への重複ダメージの防止、後方・障害物越しの攻撃の除外を実装しています。
- Stage6〜10では順序付きの紋章・複数スイッチ、予告から発動する床罠、壁で遮られる投射物などを実装しています。シーン生成・設定検証・Play Modeのチェック用コードも掲載しています。
- 宝箱は有効な候補地点からランダムに配置し、候補がない場合は固定位置を保ちます。
- GameAudioはシーンをまたいでBGM・効果音を管理します。攻撃音はAnimation Eventから、撃破音は遅延後に再生します。

## 開発環境と依存関係

元プロジェクトのUnityバージョン：**6000.3.14f1**。Unity UI、TextMesh Pro、Input Systemを使用しています。

このリポジトリ単体ではゲームを起動できません。シーン、Prefab、モデル、アニメーション素材、画像、フォント、パッケージ設定は含めていません。攻撃モーションについては、コード側の連携を掲載しています。

掲載コードは元プロジェクトからコピーし、改行と行末の空白のみ整えたものです。閲覧時は次の未収録の依存関係を前提としてください。

- StarterAssetsInputs：入力処理。
- CameraController：ステータス引き継ぎ時のカメラ設定。
- Enemy：敵へのダメージとノックバック。
- Door、Key、RareEnemy：既存のゴール判定・鍵・レア敵処理。
- TreasureChest：既存の宝箱処理。担当したランダム配置部分のみMarkdownに抜粋。
- PlayerSkills：スキル取得と討伐時の通知。担当部分のみMarkdownに抜粋。
- Inspectorで設定する参照、AnimatorとDoAttackトリガー、ステージおよびロビーのシーン。
- Resources/GameAudioのPrefab、AudioSourceと音声クリップ、ステージ生成ツールが参照する既存Prefab・素材・フォント。

Editor配下のツールはUnityEditor APIと元プロジェクトのシーン・素材を使用します。FortStageBuilderの現行コードにはStage3〜5向けの生成機能も含まれています。掲載はコード資料としてのもので、このリポジトリ単体で生成・検証を実行できる構成にはしていません。

共同制作のゲーム全体や外部素材を再配布するリポジトリではありません。


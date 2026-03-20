# unity-extensions

Some miscellaneous Unity utilities I use.

## 🚩 Installation

### VRChat Creator Companion (推奨)

1. [hrpnx](https://hrpnx.github.io/vpm-repos/) を VCC に追加
2. Manage Project から `hrpnx's Unity Extensions` を選択
3. パッケージが自動的にインストールされます

## 📌 Features

### 🥷 VRC Fallback Setter

アバタービルド時に全マテリアルの VRChat Custom Safety Fallback を**破壊的に**一括設定するコンポーネントです。

**主な機能：**

- アバター配下の全マテリアルに `VRCFallback` タグを自動設定
- 除外リストでマテリアル単位の除外が可能

**設定項目：**

| 項目           | 選択肢                                                                         | 説明                               |
| -------------- | ------------------------------------------------------------------------------ | ---------------------------------- |
| Shader Type    | Unlit, Standard, VertexLit, Toon, Particle, Sprite, Matcap, MobileToon, Hidden | フォールバック時のシェーダータイプ |
| Rendering Mode | Opaque, Cutout, Transparent, Fade                                              | レンダリングモード                 |
| Facing         | Default, DoubleSided                                                           | カリングモード                     |

これらの設定は連結されて VRCFallback タグとして設定されます (例: `ToonCutoutDoubleSided`)。

**使い方：**

1. アバタールート直下に空の GameObject を作成
2. `VRCFallbackSetter` コンポーネントを追加
3. インスペクターで Shader Type / Rendering Mode / Facing を選択
4. (任意) 除外したいマテリアルを Exclusions リストに追加
5. アバターをビルドすると自動的に適用されます

### ✨ BackLit Menu Installer

アバタービルド時に lilToon の BackLit (逆光) メニューを自動生成するコンポーネントです。

**主な機能：**

- lilToon マテリアルの BackLit パラメータを制御するアニメーションを自動生成
- Modular Avatar 経由でメニューとパラメータを自動設定
- 除外リストでマテリアル単位の除外が可能

**設定項目：**

| 項目            | 説明                               |
| --------------- | ---------------------------------- |
| Exclusions      | BackLit 設定を適用しないマテリアル |
| Default         | メニューのデフォルト状態 (ON/OFF)  |
| Saved           | パラメータを保存するかどうか       |
| Color           | BackLit の色 (HDR)                 |
| Main Strength   | メインの強さ                       |
| Normal Strength | 法線の強さ                         |
| Border          | 境界                               |
| Blur            | ぼかし                             |
| Directivity     | 指向性                             |
| View Strength   | 視点からの強さ                     |
| Receive Shadow  | 影の受け取り                       |
| Root Menu       | メニューを追加するルートメニュー   |

**使い方：**

1. アバタールート直下に空の GameObject を作成
2. `BackLitMenuInstaller` コンポーネントを追加
3. インスペクターで BackLit のパラメータを調整
4. (任意) 除外したいマテリアルを Exclusions リストに追加
5. (任意) Root Menu でメニューの追加先を指定
6. アバターをビルドすると自動的に適用されます

### 📂 BulkMat

指定フォルダ内の lilToon マテリアルに lilToon プリセットを一括適用するエディターウィンドウです。

**主な機能：**

- フォルダ内のマテリアルに lilToon プリセットを一括適用
- サブフォルダを含めるかどうかの切り替え
- lilToon 以外のシェーダーは自動スキップ
- 輪郭線シェーダー (Outline) の有効/無効をプリセットとは独立して上書き可能

**使い方：**

1. メニューから `Tools > BulkMat` を選択
2. 対象フォルダにマテリアルが含まれるフォルダを設定
3. 適用設定に lilToon プリセットを設定
4. (任意) 「輪郭線を上書き」を有効にして輪郭線の ON/OFF を指定
5. 「マテリアルに一括適用」ボタンをクリック

### 🗂️ Asset Folder Browser

`Assets/` 直下のショップ/アセットフォルダをサイズ・名前でソート・フィルタ表示するエディターウィンドウです。

**主な機能：**

- フォルダ一覧をサイズ・ショップ名・アセット名でソート
- キーワードフィルタ
- シーン参照チェック（未参照フォルダを一目で確認）
- 選択したフォルダの一括削除

**使い方：**

1. メニューから `Tools > Asset Folder Browser` を選択
2. 「シーン参照を確認」ボタンでシーンからの参照状況を取得
3. 不要なフォルダを選択して「選択した N フォルダを削除」をクリック

### 💨 CheekPuff Resetter

頬膨らみ（CheekPuff）トラッキングで PhysBone がずれる問題を自動修正するコンポーネントです。

**主な機能：**

- `CheekPuffLeft` / `CheekPuffRight` OSC パラメータが閾値を超えた瞬間に対象 PhysBone をリセット
- Modular Avatar 経由で FX レイヤーを自動生成・マージ
- 左右独立した FX レイヤー構成

**設定項目：**

| 項目        | 説明                                                             |
| ----------- | ---------------------------------------------------------------- |
| Threshold   | リセットを発動する CheekPuff パラメータの閾値 (0–1、デフォルト 0.5) |
| Cheek Bone L | リセット対象の VRCPhysBone が付いた Transform（左頬）          |
| Cheek Bone R | リセット対象の VRCPhysBone が付いた Transform（右頬）          |

**使い方：**

1. アバター配下に空の GameObject を作成
2. `CheekPuffResetter` コンポーネントを追加
3. Cheek Bone L / R に対象の PhysBone Transform を設定
4. (任意) Threshold を調整
5. アバターをビルドすると FX レイヤーが自動生成されます

**要件：**

- VRChat SDK3 Avatars（VRCPhysBone）
- Modular Avatar
- フェイストラッキング対応クライアント（OSC 経由で `CheekPuffLeft` / `CheekPuffRight` を送信）

### 🦴 Sync Clothing Bone Transforms

衣装オブジェクトのボーンにアバター本体のトランスフォームを同期するコンテキストメニューコマンドです。

**主な機能：**

- 衣装ボーンの `localPosition` / `localScale` をアバターの同名ボーンから同期
- `ModularAvatarScaleAdjuster` が付いているボーンはコンポーネントごとコピー
- Undo 対応

**使い方：**

1. アバター配下の衣装オブジェクトを選択
2. 右クリックメニューから `Sync Clothing Bone Transforms from Avatar` を選択

## 📋 Requirements

- Unity 2022.3 以上
- VRChat SDK3 Avatars
- Modular Avatar
- lilToon (BackLit Menu Installer 使用時)

## 📄 License

[MIT License](LICENSE)

## 👋 Contact

- X: [@hrpnx_vrc](https://x.com/hrpnx_vrc)

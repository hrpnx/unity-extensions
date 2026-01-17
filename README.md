# unity-extensions

Some miscellaneous Unity utilities I use.

## 🚩 Installation

### VRChat Creator Companion (推奨)

1. [hrpnx](https://hrpnx.github.io/vpm-repos/) を VCC に追加
2. Manage Project から `hrpnx's Unity Extensions` を選択
3. パッケージが自動的にインストールされます

## 📌 Features

### VRC Fallback Setter

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

### BackLit Menu Installer

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

## 📋 Requirements

- Unity 2022.3 以上
- VRChat SDK3 Avatars
- Modular Avatar
- lilToon (BackLit Menu Installer 使用時)

## 📄 License

[MIT License](LICENSE)

## 👋 Contact

- X: [@hrpnx_vrc](https://x.com/hrpnx_vrc)

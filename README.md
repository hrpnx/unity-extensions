# unity-extensions

Some miscellaneous Unity utilities I use.

## 🚩 Installation

### VRChat Creator Companion（推奨）

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

| 項目 | 選択肢 | 説明 |
|------|--------|------|
| Shader Type | Unlit, Standard, VertexLit, Toon, Particle, Sprite, Matcap, MobileToon, Hidden | フォールバック時のシェーダータイプ |
| Rendering Mode | Opaque, Cutout, Transparent, Fade | レンダリングモード |
| Facing | Default, DoubleSided | カリングモード |

これらの設定は連結されて VRCFallback タグとして設定されます（例: `ToonCutoutDoubleSided`）。

## 🔧 Usage

1. アバタールート直下に空の GameObject を作成
2. `VRCFallbackSetter` コンポーネントを追加
3. インスペクターで Shader Type / Rendering Mode / Facing を選択
4. （任意）除外したいマテリアルを Exclusions リストに追加
5. アバターをビルドすると自動的に適用されます

## 📋 Requirements

- Unity 2022.3 以上
- VRChat SDK3 Avatars
- Modular Avatar

## 📄 License

[MIT License](LICENSE)

## 👋 Contact

- GitHub: [@hrpnx](https://github.com/hrpnx)
- X: [@hrpnx_vrc](https://x.com/hrpnx_vrc)

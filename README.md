# MagicaCloth2 Collider Builder

MagicaCloth2用のコライダーを自動生成するツールです。

## Requirements

- Unity 2022.3以降
- Magica Cloth 2

Unity 6.0で動作確認しています。

## Installation

1. [Releases](https://github.com/NullClone/MagicaClothColliderBuilder/releases)からUnityパッケージをダウンロードして、Unityプロジェクトへインポートします。
2. `Tools > Magica Cloth2 > Collider Builder` を開きます。

## Usage

1. Avatar Root にアバターのTransformを設定
2. `Generate Colliders`を実行

Cleanup Existing Collidersを押すことでコライダーを削除できます。<br>
また、コライダーの生成後にSelect Collidersを押すことで生成されたコライダーを一括で選択することもできます。

## Notes

- このツールはMagica Cloth 2向けです。
- 対象アバターのルートには、有効なHumanoid Avatarを持つAnimatorが必要です。
- コライダーの生成結果はモデル、ウェイト、ボーン構造によって変わります。生成後は必ずSceneビューで位置とサイズを確認してください。
- 頭、胴体、四肢、指、つま先はそれぞれ異なるフィット設定を持ちます。必要に応じてFit ModeやAdvanced設定を調整してください。
- Custom Sourceを使う場合は、対象アバターに対応したSkinnedMeshRendererを指定してください。
- このツールはSAColliderBuilderを参考にしています。

## License

MIT Licenseです。

詳細は [LICENSE.md](LICENSE.md) を参照してください。

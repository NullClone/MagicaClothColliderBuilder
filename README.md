# MagicaCloth2 Collider Builder

MagicaCloth2用のコライダーを自動生成するツールです。

## Requirements

- Unity 6
- Magica Cloth 2

## Installation

1. [リリース](https://github.com/NullClone/MagicaClothColliderBuilder/releases)からUnityパッケージをダウンロードし、Unityにインポート
2. `Tools > Magica Cloth2 > Collider Builder` を開く

## Usage

1. Avatar Root にアバターのTransformを設定
2. `Generate Colliders`を実行

Cleanup Existing Collidersを押すことでコライダーを削除できます。<br>
また、コライダーの生成後にSelect Collidersを押すことで生成されたコライダーを一括で選択することもできます。

## Notes

- Unity 6.0での動作を確認済みです。
- 検証には標準的なアバターのみを使用しています。
- MagicaCloth2でのみで機能します。
- 必ずしも正しい生成結果になるとは限りません。
- こちらのツールはSAColliderBuilderを参考にしています。

## License

MITを採用しています。

詳細は [LICENSE.md](LICENSE.md) を参照してください。

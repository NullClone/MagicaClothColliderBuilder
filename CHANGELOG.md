# Change Log
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).


## [1.5.0] - 2026-05-03

### Added
- 現在のポーズに対しても処理できるようになりました（以前はバインドポーズのみ）
- 子ボーンのウェイトに対して閾値を設けるようにしたことで、安定性が向上しました

### Changed
- 腰のコライダー生成機能を改修し、精度を大幅に向上
- コードの可読性を向上

## [1.4.0] - 2026-04-23

### Added
- 足のコライダー生成機能を改修し、精度を大幅に向上
- Foot / Toes設定タブを追加し、足とつま先のフィット調整をUIから変更できるように

### Changed
- 指のコライダー生成を強化
- Toe fallbackのサイズと向きの計算をFitter側へ移動
- Fitter周りをリファクタリング

### Fixed
- 頭のコライダーの向きが予期しない方向になる問題を修正

### Removed
- AccuratePercentileモードを廃止し、FastBoundsに一本化

## [1.3.0] - 2026-04-22

### 手のコライダー生成を強化

### Added
- 手のひらのコライダーを生成するように
- Undo機能を実装

### Changed
- 頂点情報を格納することでパフォーマンスが大幅に向上
- プログレスバーを改良
- Fitting周りを改修

## [1.2.0] - 2026-04-22

### Added
- Fit Modeを追加し、部位ごとに調整できるように
- 体のコライダーの安定性を向上
- 指のコライダーの位置の安定性を向上

### Changed
- UIを整理し、操作性を向上

### Fixed
- 既存のコライダーも消してしまう重大な問題を修正
- CustomSourceが空のときにAuto扱いへ戻ってしまう挙動を修正

### Removed
- Reduce機能を完全に廃止

## [1.1.0] - 2026-04-21

### Added
- SkinnedMeshRendererの選択機能を追加
- 頭のコライダー処理を大幅に最適化

### Changed
- 大規模なリファクタリングを実施
- メソッドの命名を統一

### Fixed
- パッケージがインストールできない問題を修正

### Removed
- RotationSearch機能を廃止

## [1.0.1] - 2026-04-21

### Added
- プログレスバーを追加
- 頭のコライダー処理の精度を向上

## [1.0.0-alpha] - 2026-04-20

- 初回リリース

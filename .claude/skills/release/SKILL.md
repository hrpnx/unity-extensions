---
name: release
description: VPM パッケージの新バージョンをリリースするときに使用する。前回リリースからの差分をサマリ提示し、bump 種別をユーザーに確認後、package.json / package-lock.json を更新してコミット・プッシュし、ドラフトリリースを自動 Publish する。
---

# Release

このスキルは以下のフローでリリースを実行します。

## Step 1: 前回リリースからの差分をサマリ提示

以下を実行して、前回リリース以降のコミット一覧を取得する：

```bash
git log $(git describe --tags --abbrev=0)..HEAD --oneline
```

コミット一覧をユーザーに提示し、Conventional Commits のプレフィックスに基づいて bump 種別を提案する：

| コミット種別 | 推奨 bump |
|---|---|
| `feat!:` または `BREAKING CHANGE` フッターを含む | **major** |
| `feat:` を含む | **minor** |
| `fix:` / `perf:` / `refactor:` / `docs:` のみ | **patch** |

## Step 2: bump 種別をユーザーに確認

AskUserQuestion で `major` / `minor` / `patch` またはバージョン番号の直接入力を求める。

## Step 3: `package.json` と `package-lock.json` を更新

新バージョン番号を決定したら、Edit ツールで以下の箇所を書き換える：

- `package.json` の `"version"` フィールド（1箇所）
- `package-lock.json` の `"version"` フィールド（2箇所）
    - ルートの `"version": "..."` （3行目付近）
    - `"packages": { "": { "version": "..." } }` 内（9行目付近）

**`npm version` は git タグも作成するため使用しない。必ず Edit ツールで直接書き換える。**

## Step 4: コミット作成

bump コミットに加え、ステージ済みの差分（`.meta` ファイルなど）があれば一緒に含める。

```bash
git add package.json package-lock.json <その他ステージ済みファイル>
git commit -m "chore: bump version to X.Y.Z"
```

`commit-msg` フック（commitlint）が自動実行される。

## Step 5: feature ブランチを作成して push → PR 作成

**main への直接 push は禁止。** feature ブランチを切ってから push し、PR を作成する。

コミットが既に main にある場合はブランチを切り直す：

```bash
git checkout -b chore/bump-version-X.Y.Z
git checkout main && git reset --hard origin/main
git checkout chore/bump-version-X.Y.Z
```

push（push 前に確認不要）：

```bash
git push -u origin chore/bump-version-X.Y.Z
```

`pre-push` フック（csharpier / cspell / editorconfig / secretlint）が並列実行される。
フックが失敗した場合は push がキャンセルされる。lint エラーを修正してから再度 push する。

PR を作成する：

```bash
gh pr create --title "chore: bump version to X.Y.Z" --body "..." --base main --repo hrpnx/unity-extensions
```

## Step 6: CI 完了を待ってから PR をマージ

CI が通るまで待ってからマージする（確認不要で自動実行してよい）：

```bash
gh pr checks <PR番号> --repo hrpnx/unity-extensions --watch
gh pr merge <PR番号> --squash --repo hrpnx/unity-extensions
```

## Step 7: GitHub Actions 完了待ち → ドラフトリリースを Publish

マージ後、`release.yml` ワークフローの完了を待つ：

```bash
gh run watch --repo hrpnx/unity-extensions
```

完了後、作成されたドラフトリリースを Publish する：

```bash
gh release edit X.Y.Z --draft=false --repo hrpnx/unity-extensions
```

Publish をトリガーに `trigger-vpm-repos.yml` が自動実行され、VCC のパッケージリストに新バージョンが反映される。

# Copilot Instructions

## 長期 AHC (AtCoder Hueuristic Contest) ルール

I am currently participating in an AtCoder Heuristic Contest, and I will use this generative AI to assist in developing my solution.

When using this generative AI, the "AtCoder Heuristic Contest Generative AI Usage Rules - Version 20250616" apply.

https://info.atcoder.jp/entry/ahc-llm-rules-en

Most importantly, after running the solution program, you must not modify or improve the solution, its approach, or its code based on the execution results unless the user gives a new explicit instruction to do so.

You may run the solution program and report its execution results, logs, scores, or other observations. After reporting them, you must stop and wait for a new instruction from the user before making any improvement based on those results.

Here, "solution program" refers to any program created or being created for the purpose of solving this contest problem, regardless of whether it was created by the user or by generative AI, and regardless of whether it is still in progress or already complete.

## ローカルの開発ルール

- ソースコードの文字エンコードは UTF-8(BOM) とする。また、改行は CRLF とする。
- Release ビルドは提出用であり、ジャッジサーバーから標準入力を受け取るようになっている。ローカルのテストケースファイルを用いた動作確認には Debug ビルドを使用する。
- Program.cs のコードで動作が完結するようにする。環境変数や外部ファイルなどで動作を変えてはいけない。ただし、以下は参照可能。
    - .NET 標準ライブラリー
	- ac-library-csharp
	- MathNet.Numerics
	- ローカルテストケースファイル (Debug 時のみ)
	
- こちらからの指示無く計画 (Plan) を勝手に立てない。特に、ビルドや実行をステップとして分割しない。
- 以下の場合を除き、こちらの指示を最後まで完了してから対話を戻すこと。
    - 仕様の曖昧な部分を確認する
    - 複数の候補についてこちらの指示を仰ぐ
    - 実装を続行できない重大な問題がある
	
- 実装の初期から実行時間や計算量を意識した実装を行う。高度な定数倍高速化はこちらの指示を待つこと。
- Deque や SegmentTree など、Program.cs に常に置いてあるもの以外のライブラリーも存在する。必要に応じて要求すること。
- 問題に対する新しい施策の関数 (DoPlan) を実装するときは、既存の施策の動作が変わらないようにする。
- 何をやっているか、何故それをやっているかのコメントを適度に追加する。

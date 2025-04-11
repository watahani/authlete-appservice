# Authlete App Service sample

このサンプルは Authlete Server を利用した認可サーバーと、認可サーバーを利用するクライアント、リソースサーバーを App Service 上に展開するサンプルです。
展開されるアプリは以下の 3 つです。

- 認可サーバー: [authzServer](src/java-oauth-server/)
- クライアント Web アプリ: [client](src/client/)
- リソースサーバー: [api](src/api/)

authzServer は [java-oauth-server](https://github.com/authlete/java-oauth-server) で公開されている Authlete の Java リファレンス実装で、この認可サーバーは Authlete 3.0 にリクエストを転送することで認可サーバーとしての機能を実装しています。
`client` は認可サーバーのクライアントとして動作し、OpenID Connect と OAuth プロトコルで ID トークンとアクセストークンを取得します。
取得したアクセストークンはリソースサーバーである `api` に提示することで、リソースアクセスが可能となります。

認証認可部分には App Service の組み込み認証を利用しており、実装は組み込み認証の呼び出しと、組み込み認証から返却されるクレームのチェックだけで済んでいます。

## Authlete の設定

Authlete サーバーの発行するアクセストークンは既定では識別子型のトークンです。Azure では Microsoft Entra ID が内包型トークンを発行するため、多くのサービスが内包型トークン前提で動作します。
たとえば App Service 認証や、API Management の validate-jwt ポリシーなどです。

Authlete では設定次第で内包型 (正確には識別子も内包するハイブリッド型) のトークンが発行できるため、Authlete API を組み込んだ認可サーバーを簡単に Azure と統合することができます。

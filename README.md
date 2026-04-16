# rclsharp

ROS 2 と互換な通信ライブラリ

- DDS と最低限の互換性がある
- std_msgs が使える
- Unity で使える
- Unity でクロスプラットフォームビルドできる (Windows, Mac, Android, ...)

## 開発環境

Nix flake で ROS 2 Humble + .NET 8 SDK が揃います。

```sh
# Nix flake の devShell に入る (direnv 利用時は自動 reload)
nix develop
```

`ros.cachix.org` を利用するには `trusted-users` もしくは `trusted-substituters` に追加してください。未設定だと ROS パッケージを自前ビルドすることになります。

## ビルド・テスト

```sh
dotnet build
dotnet test
```

## サンプル: SPDP Demo

指定ドメインの SPDP マルチキャストに参加し、他の Participant (rclsharp 同士 / ROS 2 ノード) を検出します。

```sh
# Usage: dotnet run --project samples/SpdpDemo -- [domainId] [participantId] [entityName]
dotnet run --project samples/SpdpDemo -- 0 1 rclsharp_demo
```

## ROS 2 との相互検出確認 (loopback)

別シェルで ROS 2 talker を起動:

```sh
ROS_LOCALHOST_ONLY=1 ROS_DOMAIN_ID=0 ros2 run demo_nodes_cpp talker
```

SpdpDemo 側のログに `++ DISCOVERED ... unicast=UDPv4://127.0.0.1:7410` が出れば OK。

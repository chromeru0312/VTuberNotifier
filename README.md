# VTuber情報通知サービス: VTuberNotifier
VTuberのライブ配信やグッズなどの商品情報を通知するサービスです。    
DiscordBot、WebHook(対応予定)、WebAPI(対応予定)で取得できます。
  
※本サービスは現時点(2021/03/05)でベータ版の公開です。[v.0.2.2]  
  機能の更新や追加を行う場合、バグの発生等稼働において支障が生じた場合は予告なくサービスを停止することがあります。    
  稼働情報及び障害情報、またその他問い合わせは [Twitter](https://www.twitter.com/chromeru0312) までお願いします。

## 対応状況
### ライバー
##### 企業・グループ(公式ホームページがあるもの)
[対応済] にじさんじ / .LIVE / ぶいらいぶ / 774inc. / VOMS  
[対応予定] ホロライブ
##### その他グループ・個人ライバー
今後追加できるように変更する予定です。  
### サービス  
[対応済] [YouTube](https://www.youtube.com/) / [Booth](https://booth.pm/) / [にじさんじオフィシャルストア](https://shop.nijisanji.jp/)  
[対応予定] [ニコニコ生放送](https://live.nicovideo.jp/) / [.LIVE公式ストア](https://4693.live/)  
[対応検討中] [Bilibili](https://www.bilibili.com/) / [Twitter](https://twitter.com/)  
### 通知方法  
[対応済] DiscordBot  
[対応予定] Webhook / WebAPI  

## 使用方法
### 共通
ライバー指定方法
```
以下からひとつ選んで指定してください
name=[ライバー名(完全一致)]  
youtube=[YouTubeチャンネルID]  
twitter=[TwitterID]  
```

サービス一覧
```
youtube...YouTubeの下記全ての通知 
 -youtube_new...新規ライブ配信・動画通知  
  -youtube_new_live...新規ライブ配信通知
  -youtube_new_premiere...新規プレミア公開通知
  -youtube_new_video...新規動画通知
 -youtube_change...配信情報の変更通知  
 -youtube_delete...配信の削除通知(放送開始前) 
 -youtube_start...ライブ配信開始通知  
booth...公式Boothの下記全ての通知(存在する場合)  
 -booth_new...新商品通知  
 -booth_start...商品販売開始通知
store...公式ストアの下記全ての通知(存在する場合)  
 -store_new...新商品通知  
 -store_start...商品販売開始通知
```
### DiscordBot
Botの追加は [こちら](https://discord.com/api/oauth2/authorize?client_id=799182985600958494&permissions=133184&scope=bot) から追加できます    
情報を受信するチャンネルで以下のコマンドを打つことで操作できます。

新規追加：
```
>vinfo add <受信するライバー> <通知するサービス> [受信する情報]  
  
受信するライバー: 操作を行うライバーを指定します。(指定方法はライバー指定方法を参照)  
通知するサービス: 追加するサービス名をカンマ区切りで入力します。(種類は対応サービス一覧を参照)
受信する情報: 第1引数で指定されたライバー以外の情報を含めるかどうかを指定します。
             boolean値で入力してください。(既定値=true)
```

通知文字列変更：
```
>vinfo set <変更するライバー> <変更するイベント> [受信する情報] [変更後文字列]  
  
受信するライバー: 操作を行うライバーを指定します。(指定方法はライバー指定方法を参照)  
通知するサービス: 追加するサービス名を入力します。(種類は対応サービス一覧を参照)
                 複数入力不可、追加されてないサービスは変更できません。
受信する情報: 第1引数で指定されたライバー以外の情報を含めるかどうかを指定します。
             boolean値で入力してください。(既定値=true)
変更後文字列: 通知内容の文字列を指定、改行は\nを使用してください。 
             defaultと入力するか、未入力でデフォルト設定に変更します。
             [共通] {Date}...開始時刻  {Title}...タイトル　{URL}...URL
                    {Livers:[区切り文字]}...参加ライバー(区切り文字で区切られます)
             [YouTube共通] {VideoId}...動画ID　{ChannelId}...チャンネルID　{ChannelName}...チャンネル名
             [YouTubeChange] {OldDate}...旧開始時刻　 {ChangeDate}...時刻変更(MM/dd HH:mm → MM/dd HH:mm)
             [Booth/Store] {Id}...商品ID　{Category}...商品カテゴリー
                           {ItemsN:[区切り文字]}...商品アイテム名(区切り文字で区切られます)
                           {ItemsNP:[区切り文字]}...商品アイテム名と値段(区切り文字で区切られます)
```

削除：
```
>vinfo remove <削除するライバー> [削除するサービス]   
  
削除するライバー: 操作を行うライバーを指定します。(指定方法はライバー指定方法を参照)
通知するサービス: 追加するサービス名をカンマ区切りで入力します。(種類は対応サービス一覧を参照)
                 無入力の場合は全てのサービスの通知を削除します。  
```

### Webhook
準備中...

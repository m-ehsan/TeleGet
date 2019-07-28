# TeleGet
Dedicated Telegram Client for crawling and storing channels' messages.

This project uses [TLSharp](https://github.com/sochix/TLSharp) as an interface for Telegram API.

## Get Started
This project uses MongoDB as storage. Please make sure mongod is running at `localhost:27017`.

Get your Telegram API ID and API hash from https://my.telegram.org and add them into `TLInterface.cs` file.

Use `addsession` command and other commands below to get started.

### Commands:

#### addsession
Login to your Telegram account.
```
> addsession
Enter phone number: +989*********
Enter code: *****
Enter your cloud password: *****
Session added successfully.
```

#### add
Add a channel by username

`add durov`

#### activate
Activate a previously added channel. (get messages form this channel)

`activate durov`

#### deactivate
Deactivate a previously added channel. (don't get messages form this channel)

`deactivate durov`

#### getfeed
Start getting messages form active channels from the specified timespan.

`getfeed -from 2019-01-01 -to 2019-02-01`

#### log
View application log including number of messages that are being saved into database.

#### jobs
View pending jobs.

#### stats
View a brieft statistics on stored messages.

## To-Do
- Complete interface commands
- Complete config file
- Support multi-threading
- Support other databases

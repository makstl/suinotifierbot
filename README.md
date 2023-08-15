# SuiNotifierBot - Telegram Bot for Sui Blockchain Monitoring

Sui Notifier is a Telegram bot written in C# that notifies users about transactions and other relevant events occurring in the Sui blockchain. The bot integrates with the Telegram messaging platform, allowing users to receive real-time notifications on their Telegram accounts.

## Features

- Real-time notifications: The bot sends instant notifications to users as soon as a transaction or other relevant blockchain event occurs.
- Customizable notifications: Users have the option to choose which type of events they want to be notified about, providing greater flexibility and control over notifications.
- User-friendly commands: The bot supports a variety of simple commands that users can easily understand and interact with.
- Blockchain integration: The bot connects to the Sui blockchain, monitoring transactions and events using its APIs.

## Requirements

To run this project, you need the following installed on your machine:

- .NET Core SDK: Download (https://dotnet.microsoft.com/download)
- Telegram Bot Token: Create a bot (https://core.telegram.org/bots#3-how-do-i-create-a-bot)

## Getting Started

1. Clone the project repository:

git clone https://github.com/makstl/suinotifierbot.git


2. Navigate to the project directory:

cd suinotifierbot


3. Open the settings.json file and enter your Telegram Bot Token and other required settings.

4. Build the project:

dotnet build


5. Run the bot:

dotnet run


6. Start a conversation with your bot on Telegram and start receiving notifications!

## Usage

The bot supports the following commands:

- ✳️ New Address - Add an address to receive notifications.
- 💧 My Addresses - List and manage your addresses and it's settings
- ⚙️ Settings - Adjust your notification settings (e.g., enable/disable certain events).
- ✉️ Contact us - Send message for the crew.

## Contributing

Contributions are welcome! If you encounter any issues or have any suggestions for improvements, please open an issue (https://github.com/makstl/suinotifierbot/issues).

If you'd like to contribute, please fork the repository and create a new branch with your changes. Once you've made your changes, submit a pull request detailing the improvements you've made.

## License

This project is licensed under the MIT License (https://opensource.org/licenses/MIT). See the LICENSE file for more information.

## Acknowledgements

- This project uses the Telegram.Bot (https://github.com/TelegramBots/Telegram.Bot) library for interacting with the Telegram Bot API.

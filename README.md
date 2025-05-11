# BetterTeams

BetterTeams is a Microsoft Teams extension that adds advanced GIF management features and enhances the user experience.

## 🚀 Features

### Advanced GIF Management
- Custom GIF search with Tenor API integration
- Grid view with GIF previews
- One-click GIF copy to clipboard
- Tags and hashtags for each GIF
- Native Teams GIF popup integration

### Settings
- Dedicated settings panel
- Native Teams UI integration
- Easy access through side menu

## 🛠️ Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/BetterTeams.git
```

2. Configure API keys:
   - Get an API key from [Tenor](https://tenor.com/developer/dashboard)
   - Update `TENOR_API_KEY` in `scripts/betterteams-main.js`

3. Build using Visual Studio 2022

## 💻 Usage

### GIF Features
1. Open the GIF popup in Teams (GIF button in input field)
2. Find the new "BetterTeams GIF" section
3. Use the search bar to find specific GIFs
4. Click a GIF to copy it to clipboard
5. GIF tags are visible on hover

### Settings
1. Open Teams side menu
2. Click "BetterTeams Settings"
3. Configure available options

## 🔧 Configuration

### API Keys
```javascript
const TENOR_API_KEY = 'your_api_key_here';
const TENOR_CLIENT_KEY = 'your_client_key_here';
```

### Search Options
- Results per page: 20 GIFs
- Preview format: tinygif
- Copy format: original gif

## 🛡️ Security

- Trusted Types for XSS security
- HTML injection sanitization
- Secure event listener handling
- No sensitive data storage

## 🧪 Development

### Code Structure
```
BetterTeams/
├── scripts/
│   └── plugins/    # Extensions
│   └── betterteams-main.js    # Main code
├── manifest.json              # Extension manifest
└── README.md                  # Documentation
```

### Core Functions
- `injectHtmlWithPolicy`: Handles secure HTML injection
- `searchGifs`: Manages GIF search functionality
- `copyGifToClipboard`: Handles GIF copying
- `addGifSection`: Adds GIF section to interface
- `addSettingsButton`: Adds settings button

## 🤝 Contributing

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License. See the `LICENSE` file for details.

## 🙏 Acknowledgments

- [Tenor API](https://tenor.com/developer/dashboard) for GIF service
- Microsoft Teams for the platform
- All project contributors

## 📧 Contact

For support or questions, open an issue on GitHub or contact the development team.

---
Made with ❤️ to enhance the Teams experience

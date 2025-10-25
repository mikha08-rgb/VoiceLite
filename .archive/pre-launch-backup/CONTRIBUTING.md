# Contributing to VoiceLite

Thank you for your interest in contributing to VoiceLite! 🎉

## Ways to Contribute

### 🐛 Report Bugs
Found a bug? Please [open an issue](https://github.com/mikha08-rgb/VoiceLite/issues/new) with:
- Clear description of the problem
- Steps to reproduce
- Expected vs actual behavior
- Windows version and VoiceLite version
- Any error messages or logs

### 💡 Suggest Features
Have an idea? [Open an issue](https://github.com/mikha08-rgb/VoiceLite/issues/new) with:
- Description of the feature
- Why it would be useful
- How you envision it working

### 🔧 Submit Code Changes

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes**
   - Follow existing code style
   - Test your changes thoroughly
   - Update documentation if needed
4. **Commit with clear messages**
   ```bash
   git commit -m "Add feature: description"
   ```
5. **Push and create a Pull Request**
   ```bash
   git push origin feature/your-feature-name
   ```

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- Windows 10/11

### Build Instructions
```bash
# Clone your fork
git clone https://github.com/YOUR-USERNAME/VoiceLite.git
cd VoiceLite

# Build the solution
dotnet build VoiceLite/VoiceLite.sln

# Run the app
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj
```

### Running Tests
```bash
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj
```

## Code Style Guidelines

- Use meaningful variable and method names
- Add comments for complex logic
- Follow C# naming conventions (PascalCase for methods, camelCase for variables)
- Keep methods focused and under 50 lines when possible
- Handle errors gracefully with try-catch blocks

## Pull Request Process

1. Ensure your code builds without errors
2. Test thoroughly on Windows 10/11
3. Update CLAUDE.md if you change architecture
4. Update README.md if you add user-facing features
5. Keep PRs focused on a single feature/fix
6. Respond to review feedback promptly

## Areas That Need Help

- 🌍 Multi-language support
- 🎨 UI/UX improvements
- 📱 Additional keyboard shortcuts
- 🔊 Voice activity detection improvements
- 📝 Documentation improvements
- 🧪 More unit tests

## Questions?

- Check the [README](README.md) for general info
- Review [CLAUDE.md](CLAUDE.md) for architecture details
- Open a [Discussion](https://github.com/mikha08-rgb/VoiceLite/discussions) for questions

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

**Thank you for helping make VoiceLite better!** 🎙️
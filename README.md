# RainbowIIS

RainbowIIS is a streamlined, console-based web hosting solution, specifically designed to run Blazor applications on VM or dedicated servers during development. Its primary function is to offer a simplified and accessible alternative to Microsoft IIS for development purposes. It's important to note that RainbowIIS is intended to stay open as a console window, and closing the console will shut down the server. The application includes custom features to prevent accidental closure and to manage web hosting effectively.

## Features

- **Blazor Application Hosting**: Tailored for hosting Blazor applications in a development setting.
- **VM or Dedicated Server Compatibility**: Optimized to run on virtual machines or dedicated servers.
- **Custom Console Interface**: Prevents accidental closure and pauses, ensuring continuous server operation.
- **Automatic Crash Handling**: Includes a crash recovery system with an exponential backoff strategy.
- **Firewall Rule Automation**: Adds rules to allow traffic on all TCP ports (0-65535).
- **Port Monitoring**: Regular checks for critical ports (80, 443, 8080) for both IPv4 and IPv6.
- **Periodic Logging**: Automated logs at regular intervals, providing ongoing server status updates.
- **Administrator Privilege Enforcement**: Ensures the server runs with the necessary administrative rights.

## Requirements

- .NET 6.0 or higher.
- Windows environment.
- Understanding of web hosting principles, especially for development purposes.

## Installation

1. Clone the repository to your VM or dedicated server.
2. Install .NET 6.0 SDK if it's not already present.
3. Navigate to the cloned directory.
4. Use `dotnet build` to build the solution.
5. Run the executable as an administrator for full functionality.

## Usage

- Start the application by running the executable with administrator privileges.
- The console window will open and begin hosting your Blazor application.
- Do not close the console window; closing it will stop the server.
- Monitor the periodic logs for server status and any potential issues.

## Important Notes

- RainbowIIS is designed for development use and not intended as a production server.
- It bypasses some features of Microsoft IIS and should be used with an understanding of these differences.
- The custom console interface includes features to prevent accidental closure, such as disabling the quick edit mode and the close button.

## Contributing

Your contributions are welcome. Please fork the repository and submit pull requests with your suggested changes.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/darkwolfcreek/rainbowiis/blob/main/LICENSE) file for details.

## Support

If you encounter any issues or have questions, please open an issue on the GitHub repository.

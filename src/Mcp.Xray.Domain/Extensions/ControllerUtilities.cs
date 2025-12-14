using System;

namespace Mcp.Xray.Domain.Extensions
{
    /// <summary>
    /// Provides utility methods for controllers in the G4™ application.
    /// </summary>
    public static class ControllerUtilities
    {
        /// <summary>
        /// Writes the ASCII logo to the console, including the specified version number.
        /// </summary>
        /// <param name="version">The version number to display in the logo.</param>
        public static void WriteAsciiLogo(string version)
        {
            // Define the ASCII art logo with placeholders for version information.
            var logo = new string[]
            {
                "   ____ _  _    __  ______             __  __  ____ ____       ",
                "  / ___| || |   \\ \\/ /  _ \\ __ _ _   _|  \\/  |/ ___|  _ \\ ",
                " | |  _| || |_   \\  /| |_) / _` | | | | |\\/| | |   | |_) |   ",
                " | |_| |__   _|  /  \\|  _ < (_| | |_| | |  | | |___|  __/     ",
                "  \\____|  |_|   /_/\\_\\_| \\_\\__,_|\\__, |_|  |_|\\____|_|  ",
                "                                 |___/                         ",
                "                                                               ",
                "                                    G4™ - XRay MCP Service     ",
                "                                                               ",
                "  Version: " + version + "                                     ",
                "  Project: https://github.com/g4-api                           ",
                "                                                               "
             };

            // Clear the console before writing the logo to ensure a clean display.
            Console.Clear();

            // Set the console output encoding to UTF-8 to support Unicode characters.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Output the logo to the console by joining the array elements with a newline character.
            Console.WriteLine(string.Join("\n", logo));
        }
    }
}

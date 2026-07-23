using System.Windows.Forms;
using Client.Main;

#if DEBUG
Constants.DataPath = System.Environment.GetEnvironmentVariable("MU_DATA_PATH")
    ?? @"C:\Games\MU_Red_1_20_61_Full\Data";
#endif

Application.SetHighDpiMode(HighDpiMode.SystemAware);

using var game = new MuGame();
game.Run();

global using Avalonia.Headless.XUnit;
global using Xunit;

using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Pretext.Avalonia.Tests.TestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]

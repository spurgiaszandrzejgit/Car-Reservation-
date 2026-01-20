// Decompiled with JetBrains decompiler
// Type: vaurioajoneuvo_finder.Properties.Settings
// Assembly: vaurioajoneuvo_finder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D94E05F5-0C4E-483F-B409-5B78332FAE36
// Assembly location: C:\Users\andre\OneDrive\Pulpit\1\vaurioajoneuvo_finder.exe

using System.CodeDom.Compiler;
using System.Configuration;
using System.Runtime.CompilerServices;

#nullable disable
namespace vaurioajoneuvo_finder.Properties;

[CompilerGenerated]
[GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.6.0.0")]
internal sealed class Settings : ApplicationSettingsBase
{
  private static Settings defaultInstance = (Settings) SettingsBase.Synchronized((SettingsBase) new Settings());

  public static Settings Default
  {
    get
    {
      Settings defaultInstance = Settings.defaultInstance;
      return defaultInstance;
    }
  }
}

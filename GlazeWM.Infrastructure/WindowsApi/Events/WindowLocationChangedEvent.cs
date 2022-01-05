﻿using System;
using GlazeWM.Infrastructure.Bussing;

namespace GlazeWM.Infrastructure.WindowsApi.Events
{
  public class WindowLocationChangedEvent : Event
  {
    public IntPtr WindowHandle { get; }

    public WindowLocationChangedEvent(IntPtr windowHandle)
    {
      WindowHandle = windowHandle;
    }
  }
}

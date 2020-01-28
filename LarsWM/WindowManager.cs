using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static LarsWM.WindowsApi.WindowsApiService;
using static LarsWM.WindowsApi.WindowsApiFacade;

namespace LarsWM
{
    enum FocusDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    class WindowManager
    {
        // TODO: change to behaviour subject
        // On addition of a monitor, assign it a workspace
        // On removal of a workspace, move its workspaces to a different monitor
        private List<Monitor> _monitors = new List<Monitor>();

        public WindowManager()
        {
            // Create a Monitor instance for each Screen
            foreach (var screen in Screen.AllScreens)
            {
                _monitors.Add(new Monitor(screen));
            }

            var focusedMonitor = _monitors.Find(m => m.IsPrimary);

            // Create an initial Workspace for each Monitor
            int index = 0;
            foreach (var monitor in _monitors)
            {
                // TODO: add IsFocused property to focused window, workspace & monitor
                var newWorkspace = new Workspace(index, new List<Window>());
                monitor.WorkspacesInMonitor.Add(newWorkspace);
                monitor.DisplayedWorkspace = newWorkspace;

                index++;
            }

            var windows = GetOpenWindows();

            foreach (var window in windows)
            {
                // Debug log
                DumpManagedWindows(window);

                // Add window to its nearest workspace
                var targetMonitor = GetMonitorFromWindowHandle(window);
                targetMonitor.DisplayedWorkspace.WindowsInWorkspace.Add(window);
            }

            foreach (var monitor in _monitors)
            {
                // Force initial layout
                var windowsInMonitor = monitor.DisplayedWorkspace.WindowsInWorkspace;
                // TODO: filter out windows that can not be laid out
                //var moveableWindows = windowsInMonitor.Where(w => w.CanLayout) as List<Window>;

                var windowLocations = LayoutService.CalculateInitialLayout(monitor, windowsInMonitor);

                var handle = BeginDeferWindowPos(windows.Count());

                for (var i = 0; i < windowLocations.Count() - 1; i++)
                {
                    var window = windows[i];
                    var loc = windowLocations[i];

                    var adjustedLoc = new WindowLocation(loc.X + monitor.X, loc.Y + monitor.Y, 
                        loc.Width, loc.Height);

                    var flags = SWP.SWP_FRAMECHANGED | SWP.SWP_NOACTIVATE | SWP.SWP_NOCOPYBITS |
                        SWP.SWP_NOZORDER | SWP.SWP_NOOWNERZORDER;

                    DeferWindowPos(handle, window.Hwnd, IntPtr.Zero, adjustedLoc.X, adjustedLoc.Y, adjustedLoc.Width, adjustedLoc.Height, flags);
                }

                EndDeferWindowPos(handle);
            }

            Debug.WriteLine(_monitors);
        }

        private static void DumpManagedWindows(Window window)
        {
            StringBuilder sb = new StringBuilder(GetWindowTextLength(window.Hwnd) + 1);
            GetWindowText(window.Hwnd, sb, sb.Capacity);
            Debug.WriteLine(sb.ToString());

            uint processId;
            GetWindowThreadProcessId(window.Hwnd, out processId);
            var _processId = (int)processId;

            var process = Process.GetProcesses().FirstOrDefault(p => p.Id == _processId);
            var _processName = process.ProcessName;
            Debug.WriteLine(_processName);
            Debug.WriteLine(window.CanLayout);
        }

        public Monitor GetMonitorFromWindowHandle(Window window)
        {
            var screen = Screen.FromHandle(window.Hwnd);
            return _monitors.FirstOrDefault(m => m.Screen.DeviceName == screen.DeviceName) ?? _monitors[0];
        }

        public void ShiftFocusInDirection(FocusDirection direction)
        { }

        public void ShiftFocusToWorkspace(Workspace workspace)
        { }

        public void MoveFocusedWindowToWorkspace(Window window, Workspace workspace)
        { } 

        public void MoveFocusedWindowInDirection(Window window, FocusDirection direction)
        { }

    }
}


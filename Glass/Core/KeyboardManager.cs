using Glass.Controls;
using Glass.Data.Models;
using Glass.Data.Repositories;
using Glass.Input;
using System.Collections.Generic;
using System.Windows;

namespace Glass.Core;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyboardManager
//
// Owns all keyboard activity for the active profile.
// Creates and manages HidKeyInput, routes key events to commands based on
// the active page per device instance, and manages OSD windows.
// HidKeyInput is started on LoadProfile and stopped on UnloadProfile.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class KeyboardManager
{
    private HidKeyInput? _hidKeyInput;

    // Active page per device instance
    private readonly Dictionary<HidDeviceInstance, KeyPage> _activePages = new();

    // All pages for the active profile, keyed by (device instance, page name)
    private readonly Dictionary<(HidDeviceInstance Instance, string PageName), KeyPage> _pageCache = new();

    // Bindings per page ID
    private readonly Dictionary<int, List<KeyBinding>> _bindingCache = new();

    // Commands keyed by command ID
    private readonly Dictionary<int, Command> _commandCache = new();

    // OSD windows keyed by device instance — created on LoadProfile, shown on trigger
    private readonly Dictionary<HidDeviceInstance, KeyboardOsdWindow> _osdWindows = new();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // KeyboardManager
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public KeyboardManager()
    {
        DebugLog.Write("KeyboardManager: initialized.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // LoadProfile
    //
    // Loads pages and bindings for the given profile.
    // Creates HidKeyInput, starts device readers, creates OSD windows.
    // Sets the start page as active for each device instance.
    //
    // profileName:  The profile to load
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void LoadProfile(string profileName)
    {
        DebugLog.Write($"KeyboardManager.LoadProfile: profileName='{profileName}'.");

        UnloadProfile();

        var profileRepo = new ProfileRepository(profileName);
        int profileId = profileRepo.GetId();

        if (profileId == 0)
        {
            DebugLog.Write($"KeyboardManager.LoadProfile: profile '{profileName}' not found.");
            return;
        }

        var profilePageRepo = new ProfilePageRepository();
        var profilePages = profilePageRepo.GetPagesForProfile(profileId);

        if (profilePages.Count == 0)
        {
            DebugLog.Write($"KeyboardManager.LoadProfile: no pages assigned to profile '{profileName}'.");
            return;
        }

        var pageRepo = new KeyPageRepository();
        var bindingRepo = new KeyBindingRepository();
        var commandRepo = new CommandRepository();

        foreach (var command in commandRepo.GetAllCommands())
        {
            _commandCache[command.Id] = command;
        }

        foreach (var profilePage in profilePages)
        {
            var page = pageRepo.GetPage(profilePage.KeyPageId);
            if (page == null)
            {
                DebugLog.Write($"KeyboardManager.LoadProfile: page id={profilePage.KeyPageId} not found, skipping.");
                continue;
            }

            var bindings = bindingRepo.GetBindingsForPage(page.Id);
            _bindingCache[page.Id] = bindings;

            // For now use instance 1 for all device types
            var instance = new HidDeviceInstance(page.Device, 1, string.Empty);
            _pageCache[(instance, page.Name)] = page;

            if (profilePage.IsStartPage)
            {
                _activePages[instance] = page;
                DebugLog.Write($"KeyboardManager.LoadProfile: start page for {instance} is '{page.Name}'.");

                CreateOsdWindow(instance, page);
            }
        }

        DebugLog.Write($"KeyboardManager.LoadProfile: loaded {profilePages.Count} pages {_commandCache.Count} commands.");

        _hidKeyInput = new HidKeyInput();
        _hidKeyInput.KeyStateChanged += OnKeyStateChanged;
        _hidKeyInput.Start();

        DebugLog.Write("KeyboardManager.LoadProfile: HidKeyInput started.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // UnloadProfile
    //
    // Stops HidKeyInput, closes OSD windows, and clears all cached data.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void UnloadProfile()
    {
        DebugLog.Write("KeyboardManager.UnloadProfile: unloading.");

        if (_hidKeyInput != null)
        {
            _hidKeyInput.KeyStateChanged -= OnKeyStateChanged;
            _hidKeyInput.Stop();
            _hidKeyInput = null;
            DebugLog.Write("KeyboardManager.UnloadProfile: HidKeyInput stopped.");
        }

        foreach (var osd in _osdWindows.Values)
        {
            osd.Close();
        }
        _osdWindows.Clear();

        _activePages.Clear();
        _pageCache.Clear();
        _bindingCache.Clear();
        _commandCache.Clear();

        DebugLog.Write("KeyboardManager.UnloadProfile: complete.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ToggleOsd
    //
    // Shows or hides the OSD window for the given device instance.
    //
    // instance:  The device instance whose OSD to toggle
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void ToggleOsd(HidDeviceInstance instance)
    {
        DebugLog.Write($"KeyboardManager.ToggleOsd: {instance}.");

        if (!_osdWindows.TryGetValue(instance, out var osd))
        {
            DebugLog.Write($"KeyboardManager.ToggleOsd: no OSD for {instance}.");
            return;
        }

        if (osd.IsVisible)
        {
            osd.Hide();
            DebugLog.Write($"KeyboardManager.ToggleOsd: hidden.");
        }
        else
        {
            osd.Show();

            if (_activePages.TryGetValue(instance, out var page))
            {
                PushOsdData(instance, page);
            }
            DebugLog.Write($"KeyboardManager.ToggleOsd: shown.");
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CreateOsdWindow
    //
    // Creates an OSD window for the given device instance and page.
    // The window is created hidden — shown only when triggered.
    //
    // instance:  The device instance
    // page:      The start page for this instance
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void CreateOsdWindow(HidDeviceInstance instance, KeyPage page)
    {
        DebugLog.Write($"KeyboardManager.CreateOsdWindow: {instance} page='{page.Name}'.");

        var osd = new KeyboardOsdWindow(page.Device);
        _osdWindows[instance] = osd;

        PushOsdData(instance, page);

        DebugLog.Write($"KeyboardManager.CreateOsdWindow: created for {instance}.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // PushOsdData
    //
    // Builds a KeyDisplay dictionary for the given page and pushes it to the OSD window.
    //
    // instance:  The device instance
    // page:      The page to display
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void PushOsdData(HidDeviceInstance instance, KeyPage page)
    {
        if (!_osdWindows.TryGetValue(instance, out var osd))
        {
            return;
        }

        if (!_bindingCache.TryGetValue(page.Id, out var bindings))
        {
            return;
        }

        var keys = new Dictionary<string, KeyDisplay>();

        foreach (var binding in bindings)
        {
            string label = "-";

            if (binding.CommandId.HasValue && _commandCache.TryGetValue(binding.CommandId.Value, out var command))
            {
                label = string.IsNullOrWhiteSpace(command.ShortName) ? command.Name : command.ShortName;
            }

            keys[binding.Key] = new KeyDisplay
            {
                KeyName = binding.Key,
                Label = label,
                KeyType = KeyType.Momentary
            };
        }

        osd.SetPage(page.Name, keys);

        DebugLog.Write($"KeyboardManager.PushOsdData: pushed {keys.Count} keys for page='{page.Name}'.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnKeyStateChanged
    //
    // Fires when a key is pressed or released.
    // Routes press events to command execution based on the active page.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void OnKeyStateChanged(object? sender, HidKeyEventArgs e)
    {
        if (!e.IsPressed)
        {
            return;
        }

        if (!e.Device.HasValue)
        {
            return;
        }

        HidDeviceInstance instance = e.Device.Value;

        if (!_activePages.TryGetValue(instance, out var activePage))
        {
            DebugLog.Write($"KeyboardManager.OnKeyStateChanged: no active page for {instance}.");
            return;
        }

        if (!_bindingCache.TryGetValue(activePage.Id, out var bindings))
        {
            return;
        }

        var binding = bindings.FirstOrDefault(b => b.Key == e.KeyName);

        if (binding == null)
        {
            DebugLog.Write($"KeyboardManager.OnKeyStateChanged: no binding for key='{e.KeyName}' on page='{activePage.Name}'.");
            return;
        }

        if (!binding.CommandId.HasValue)
        {
            return;
        }

        if (!_commandCache.TryGetValue(binding.CommandId.Value, out var command))
        {
            return;
        }

        DebugLog.Write($"KeyboardManager.OnKeyStateChanged: key='{e.KeyName}' page='{activePage.Name}' command='{command.Name}' target='{binding.Target}'.");

        // TODO: execute command via pipe
    }
}

#pragma once
#include <windows.h>
#include <d3d11.h>
#include <string>
#include <map>
#include <mutex> 
#include "Rendering\QuadRenderer.h"
#include "Capture\SessionCapture.h"

typedef unsigned int SlotID;

// Describes a single captured session and its destination rectangle
// within the GlassVideo window.
struct SlotInfo
{
    SlotID         slotId = 0;
    std::string    sessionName;
    HWND           hwnd = NULL;
    int            x = 0;
    int            y = 0;
    int            width = 0;
    int            height = 0;
    SessionCapture capture;
};

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// RegionSource
//
// Defines a named source subregion within an EQ client window.
// Coordinates are absolute pixel coordinates captured from the live client.
//
// name:    Name used to match against a RegionDest with the same name
// x, y:    Top-left corner of the source region in client window pixels
// width:   Width of the source region in pixels
// height:  Height of the source region in pixels
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
struct RegionSource
{
    std::string name;
    int         x;
    int         y;
    int         width;
    int         height;
};

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// RegionDest
//
// Defines a named destination region relative to a slot's origin.
// Coordinates are slot-relative and may be negative or exceed slot bounds.
//
// name:    Name used to match against a RegionSource with the same name
// x, y:    Top-left corner relative to the slot's origin, in pixels
// width:   Width of the destination region in pixels
// height:  Height of the destination region in pixels
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
struct RegionDest
{
    std::string name;
    int         x;
    int         y;
    int         width;
    int         height;
};

typedef std::map<std::string, RegionSource>  RegionSourceMap;
typedef std::map<std::string, RegionDest>    RegionDestMap;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// SlotManager
//
// Manages the collection of active capture slots.
// Thread-safe — Add/Remove/Clear may be called from the pipe thread
// while Capture/Render are called from the render loop thread.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

class SlotManager
{
public:
    // Defines a slot's position in the GlassVideo window.
    // Creates the slot if it does not exist, updates position if it does.
    void Define(SlotID slotId, int x, int y, int width, int height);

    // Assigns a capture session to an existing slot.
    // Initializes WGC capture for the given HWND.
    void Assign(ID3D11Device* device, SlotID slotId, const std::string& sessionName, HWND hwnd);

    // Removes all sessions assigned to a slot and removes the slot definition.
    void Remove(SlotID slotID);

    // Removes a specific session assignment from a slot, leaving the slot definition intact.
    void Unassign(const std::string& sessionName);

    // Removes all slots and shuts down all captures.
    void Clear();

    // Calls Capture() on all slots. Should be called from the render loop.
    void CaptureAll();

    // Returns the HWND of the slot containing the given point, or NULL if no slot contains it.
    HWND HitTest(int x, int y);

    const std::multimap<SlotID, std::unique_ptr<SlotInfo>>& GetSlots() const;
    std::mutex& GetMutex();

    void DefineSource(const std::string& name, int x, int y, int width, int height);
    void DefineDestination(const std::string& name, int x, int y, int width, int height);
    void ClearRegions();

    const RegionSourceMap& GetSources() const;
    const RegionDestMap& GetDestinations() const;

private:
    std::multimap<SlotID, std::unique_ptr<SlotInfo>> _slots;
    std::mutex                                       _mutex;
    RegionSourceMap                                  _sources;
    RegionDestMap                                    _destinations;
};
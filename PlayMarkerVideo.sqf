disableSerialization;
params [["_file", "Video.sqf"], ["_markerType", "mil_dot"], ["_skipPreprocessing", false]];

diag_log "Load Video";

JK_frames = call compileScript [_file];

diag_log "Video Loaded";

JK_skipPreprocessing = _skipPreprocessing;

JK_width = JK_frames select 0 select 0;
JK_height = JK_frames select 0 select 1;
JK_frameRate = JK_frames select 0 select 2;

diag_log "Create Color Hashmap";
JK_colorMap = createHashMap;
{
    JK_colorMap set [_x select 0, _x select 1];
} forEach (JK_frames select 1);

JK_TokenRegex = (keys JK_colorMap) joinString "|";
JK_frames = JK_frames select 2;

JK_frameCount = count JK_frames;

JK_prevFrameData = [];
JK_prevFrameIndex = -1;

JK_FrameIndex = 0;
JK_PlayTime = 0;

JK_markers = [];

diag_log "Create Markers";
for "_y" from 1 to JK_height do {
    for "_x" from 1 to JK_width do {
        private _pos = [_x, _y];
        _pos = _pos vectorMultiply [-1, -1];
        _pos = _pos vectorAdd [JK_width, JK_height];
        _pos = _pos vectorMultiply [10, 10];

        private _marker = createMarkerLocal [format["%1_%2", _x, _y], _pos];
        _marker setMarkerTypeLocal _markerType;
        _marker setMarkerColorLocal "ColorBlack";
        _marker setMarkerShadowLocal false;
        JK_markers pushBack _marker;
    };
};
diag_log "Marker Created";


if (JK_skipPreprocessing) then {
    diag_log "Skipped Preprocessing data";
} else {
        diag_log "PreProcess Frame Data";
    JK_frames = JK_frames apply {
        if (_x isEqualType 0) then {
            _x
        } else {
            private _numberValues = (_x regexFind ["[0-9]+"]) apply {parseNumber (_x select 0 select 0)};
            private _values = (_x regexFind [JK_TokenRegex]) apply {JK_colorMap get (_x select 0 select 0)};
            [_numberValues, _values]
        };
    };
    diag_log "Frame Data PreProcessed";
};

private _map = ((findDisplay 12) displayCtrl 51);

_map ctrlAddEventHandler ["Draw", {
    JK_PlayTime = JK_PlayTime + diag_deltaTime;
    JK_FrameIndex = JK_PlayTime * JK_frameRate;
    if (JK_FrameIndex >= JK_frameCount) then {
        JK_FrameIndex = 0;
        JK_PlayTime = 0;
    };
    if (JK_prevFrameIndex isEqualTo JK_FrameIndex) exitWith {};
    JK_prevFrameIndex = JK_FrameIndex;

    private _frameData = JK_frames select JK_FrameIndex;
    if (JK_prevFrameData isEqualTo _frameData) exitWith {};
    JK_prevFrameData = _frameData;

    if (_frameData isEqualType 0) then {
        _frameData = JK_frames select _frameData;
    };

    if (JK_skipPreprocessing) then {
        private _numberValues = (_frameData regexFind ["[0-9]+"]) apply {parseNumber (_x select 0 select 0)};
        private _values = (_frameData regexFind [JK_TokenRegex]) apply {JK_colorMap get (_x select 0 select 0)};
        _frameData = [_numberValues, _values];
    };

    _frameData params ["_numberValues", "_values"];

    private _idx = 0;
    {
        private _color = _values select _forEachIndex;
        for "_i" from 0 to _x - 1 do {
            (JK_markers select _idx) setMarkerColorLocal _color;
            _idx = _idx + 1;
        };
    } forEach _numberValues;
}];

addMissionEventHandler ["Map", {
    params ["_mapIsOpen"];
    if (_mapIsOpen) then {
        private _map = ((findDisplay 12) displayCtrl 51);
        _map ctrlMapAnimAdd [0, 0.16, [1092.48,782.646]];
        ctrlMapAnimCommit _map;
        0 spawn {
            JK_FrameIndex = 0;
            JK_PlayTime = 0;
            playMusic ["Interstella5555", JK_PlayTime];
        };
    } else {
        playMusic "";
    };
}];
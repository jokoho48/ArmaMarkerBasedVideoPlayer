disableSerialization;
params [["_file", "Video.sqf"], ["_markerType", "mil_dot"]];
JK_frames = call compileScript [_file];

JK_width = JK_frames select 0 select 0;
JK_height = JK_frames select 0 select 1;
JK_frameRate = JK_frames select 0 select 2;

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

JK_frames = JK_frames apply {
    if (_x isEqualType 0) then {
        _x
    } else {
        private _numberValues = (_x regexFind ["[0-9]+"]) apply {parseNumber (_x select 0 select 0)};
        private _values = (_x regexFind [JK_TokenRegex]) apply {JK_colorMap get (_x select 0 select 0)};
        [_numberValues, _values]
    };
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

    private _ctrl = _this select 0;
    private _frameData = JK_frames select JK_FrameIndex;

    if (JK_prevFrameData isEqualTo _frameData) exitWith {};
    JK_prevFrameData = _frameData;

    if (_frameData isEqualType 0) then {
        _frameData = JK_frames select _frameData;
    };

    private _numberValues = _frameData select 0;
    private _values = _frameData select 1;
    private _idx = 0;
    {
        private _color = _value select _forEachIndex;
        for "_i" from 0 to _x - 1 do {
            (JK_markers select _idx) setMarkerColorLocal _color;
            _idx = _idx + 1;
        };
    } forEach _numberValues;


}];

addMissionEventHandler ["Map", {
    private _map = ((findDisplay 12) displayCtrl 51);
    _map ctrlMapAnimAdd [0, 0.16, [1092.48,782.646]];
    ctrlMapAnimCommit _map;
    0 spawn {
        playMusic "RickRoll";
        JK_FrameIndex = 0;
        JK_PlayTime = 0;
    };
}];
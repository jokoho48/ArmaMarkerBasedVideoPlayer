disableSerialization;

JK_frames = call compileScript ["BadApple.sqf"];

JK_width = JK_frames select 0 select 0;
JK_height = JK_frames select 0 select 1;

JK_frames = JK_frames select 1;

JK_frameCount = count JK_frames;

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
        _marker setMarkerTypeLocal "mil_dot";
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
        private _values = (_x regexFind ["t|f"]) apply {["ColorBlack", "ColorWhite"] select (_x select 0 select 0 == "t")};
        [_numberValues, _values]
    };
};

private _map = ((findDisplay 12) displayCtrl 51);

_map ctrlAddEventHandler ["Draw", {
    private _ctrl = _this select 0;
    private _frameData = JK_frames select JK_FrameIndex;
    if (_frameData isEqualType 0) then {
        _frameData = JK_frames select _frameData;
    };

    private _numberValues = _frameData select 0;
    private _values = _frameData select 1;
    private _idx = 0;
    {
        for "_i" from 0 to _x - 1 do {
            (JK_markers select _idx) setMarkerColorLocal (_values select _forEachIndex);
            _idx = _idx + 1;
        };
    } forEach _numberValues;

    JK_PlayTime = JK_PlayTime + diag_deltaTime;
    JK_FrameIndex = JK_PlayTime * 30;
    if (JK_FrameIndex >= JK_frameCount) then {
        JK_FrameIndex = 0;
        JK_PlayTime = 0;
    };
}];

addMissionEventHandler ["Map", {
    private _map = ((findDisplay 12) displayCtrl 51);
    _map ctrlMapAnimAdd [0, 0.16, [1092.48,782.646]];
    ctrlMapAnimCommit _map;
    0 spawn {
        sleep 1;
        JK_FrameIndex = 0;
        JK_PlayTime = 0;

        playMusic "BadApple";
    };
}];
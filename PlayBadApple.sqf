disableSerialization;

JK_frames = call compile loadFile "BadApple.sqf";

JK_width = JK_frames select 0 select 0;
JK_height = JK_frames select 0 select 1;

JK_frames = JK_frames select 1;

JK_FrameIndex = 0;

private _map = ((findDisplay 12) displayCtrl 51);

_map ctrlAddEventHandler ["Draw", {
    private _ctrl = _this select 0;
    private _frameStr = JK_frames select JK_FrameIndex;
    if (_frameStr isEqualType 0) then {
        _frameStr = JK_frames select _frameStr;
    };

    private _xPos = 0;
    private _yPos = 0;
    private _numberValues = (_frameStr regexFind ["[0-9]+"]) apply {parseNumber (_x select 0 select 0)};
    private _values = (_frameStr regexFind ["t|f"]) apply {[[0, 0, 0, 1], [1, 1, 1, 1]] select (_x select 0 select 0 == "t")};
    {
        for "_i" from 0 to _x-1  do {
            private _pos = [_xPos, _yPos];
            
            _pos = _pos vectorMultiply [-1, -1];
            _pos = _pos vectorAdd [JK_width, JK_height];
            _pos = _pos vectorMultiply [10, 10];

            private _value = _values select _forEachIndex;
            _ctrl drawIcon ["\a3\3den\data\cfg3den\marker\iconellipse_ca.paa", _value, _pos, 10, 10, 0];
            _xPos = _xPos + 1;
            if (_xPos == JK_width) then {
                _xPos = 0;
                _yPos = _yPos + 1;
            };
        };
    } forEach _numberValues;

    JK_FrameIndex = JK_FrameIndex + 1;
    if (JK_FrameIndex >= count JK_frames) then {
        JK_FrameIndex = 0;
    };
}];

addMissionEventHandler ["Map", {
    private _map = ((findDisplay 12) displayCtrl 51);
    _map ctrlMapAnimAdd [0, 0.16, [436.507,368.001]];
    ctrlMapAnimCommit _map;
    0 spawn {
        sleep 1;
        JK_FrameIndex = 0;
    };
}];
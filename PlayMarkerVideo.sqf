disableSerialization;
params [["_file", "Video.sqf"], ["_markertype", "mil_dot"], ["_skipPreprocessing", false], ["_musicTrack", ""], ["_mapZoom", 0.111628], ["_mapPos", [888.629, 501.534]]];

diag_log "load Video";
JK_MusicTrack = _musicTrack;
JK_frames = call compileScript [_file];

diag_log "Video loaded";

JK_skipPreprocessing = _skipPreprocessing;

JK_width = JK_frames select 0 select 0;
JK_height = JK_frames select 0 select 1;
JK_frameRate = JK_frames select 0 select 2;

diag_log "Create Color Hashmap";
JK_colorMap = createHashMap;

{
    JK_colorMap set [_x select 0, _x select 1];
} forEach (JK_frames select 1);

JK_tokenRegex = (keys JK_colorMap) joinstring "|";
JK_frames = JK_frames select 2;

JK_framecount = count JK_frames;

JK_prevFrameData = [];
JK_prevFrameindex = -1;

JK_Frameindex = 0;
JK_Playtime = 0;

JK_mapZoom = _mapZoom;
JK_mapPos = _mapPos;

JK_markers = [];

isNil {

    diag_log "Create Markers";

    for "_y" from 1 to JK_height do {
        for "_x" from 1 to JK_width do {
            private _pos = [_x, _y];
            _pos = _pos vectorMultiply [1, -1];
            _pos = _pos vectorAdd [0, JK_height];
            _pos = _pos vectorMultiply [10, 10];

            private _marker = createMarkerlocal [format["%1_%2", _x, _y], _pos];
            _marker setMarkerTypeLocal _markertype;
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
                private _numberValues = (_x regexFind ["[0-9]+"]) apply {
                    parseNumber (_x select 0 select 0)
                };
                private _values = (_x regexFind [JK_tokenRegex]) apply {
                    JK_colorMap get (_x select 0 select 0)
                };
                [_numberValues, _values]
            };
        };
        diag_log "Frame Data PreProcessed";
    };
};

addMissionEventHandler ["Map", {
	params ["_mapisOpen"];

	private _map = ((findDisplay 12) displayCtrl 51);
	if (_mapisOpen) then {
		_map ctrlMapAnimAdd [0, JK_mapZoom, JK_mapPos];
		ctrlMapAnimCommit _map;
		JK_Frameindex = 0;
		JK_Playtime = 0;
		if (JK_MusicTrack != "") then {
			playMusic [JK_MusicTrack, JK_Playtime];
		};
	} else {
		playMusic "";
		JK_mapPos = _map ctrlMapScreenToWorld [0.5, 0.5];
		JK_mapZoom = ctrlMapScale _map;
		copyToClipboard str [0, JK_mapZoom, JK_mapPos];
	};
}];

waitUntil {!(isNull (findDisplay 12))};

private _map = ((findDisplay 12) displayCtrl 51);

_map ctrlAddEventHandler ["Draw", {
    JK_Playtime = JK_Playtime + diag_deltatime;
    JK_Frameindex = JK_Playtime * JK_frameRate;
    if (JK_Frameindex >= JK_framecount) then {
        JK_Frameindex = 0;
        JK_Playtime = 0;
    };
    if (JK_prevFrameindex isEqualto JK_Frameindex) exitwith {};
    JK_prevFrameindex = JK_Frameindex;
    private _frameData = JK_frames select JK_Frameindex;
    if (JK_prevFrameData isEqualto _frameData) exitwith {};
    JK_prevFrameData = _frameData;
    if (_frameData isEqualtype 0) then {
        _frameData = JK_frames select _frameData;
    };
    
    if (JK_skipPreprocessing) then {
        private _numberValues = (_frameData regexFind ["[0-9]+"]) apply {
            parseNumber (_x select 0 select 0)
        };
        private _values = (_frameData regexFind [JK_tokenRegex]) apply {
            JK_colorMap get (_x select 0 select 0)
        };
        _frameData = [_numberValues, _values];
    };
    
    _frameData params ["_numberValues", "_values"];

    private _idx = 0;
    {
        private _color = _values select _forEachindex;
        for "_i" from 0 to _x - 1 do {
            (JK_markers select _idx) setMarkerColorLocal _color;
            _idx = _idx + 1;
        };
    } forEach _numberValues;
}];
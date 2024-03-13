private _output = "[";
private _first = true;
{
    private _color = getArray (_x >> "color");
    if !(_color isEqualTypeAll 0) then {
        continue;
    };
    if (_first) then {
        _first = false;
    } else {
        _output = _output + ",";
    };
    _output = _output + "{";
    _output = _output + """SerializedName"": ""REPLACE_ME"",";

    _output = _output + format ["""MarkerName"": ""%1"",", configName _x];
    
    _output = _output + format ["""Red"": %1,", _color select 0];
    _output = _output + format ["""Green"": %1,", _color select 1];
    _output = _output + format ["""Blue"": %1", _color select 2];
    _output = _output + "}";
} foreach ("true" configClasses (configFile >> "CfgMarkerColors"));
_output = _output + "]";
copyToClipboard _output
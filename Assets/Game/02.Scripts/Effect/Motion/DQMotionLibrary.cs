using System;
using System.Collections.Generic;
using UnityEngine;

// Visual data only. Gameplay CharacterData.Motion remains the combat timing source.
public sealed class DQMotionLibrary
{
    private readonly Dictionary<string, DQActorClips> _actorDict = new();
    public IReadOnlyDictionary<string, DQActorClips> Actors => _actorDict;

    public DQMotionLibrary(ResourceService resources, SpriteAtlasService atlas)
    {
        var catalog = resources.LoadJson<DQClipCatalog>("Data/DQ/dq_character_clips");
        foreach (var actor in catalog.actors)
        {
            _actorDict.Add(actor.actorId, new DQActorClips(actor, atlas));
        }
    }

    public DQActorClips Get(string actor) => _actorDict.TryGetValue(actor, out var clips) ? clips : null;

    public bool TryLegacy(string key, out Sprite sprite)
    {
        sprite = null;
        var split = key.IndexOf('_');
        var id = split < 0 ? key : key.Substring(0, split);
        if (!_actorDict.TryGetValue(id, out var actor))
        {
            return false;
        }
        var suffix = split < 0 ? "" : key.Substring(split + 1);
        var direction = suffix switch
        {
            "down" => 0,
            "front" or "downright" or "" or "telegraph" or "inflate" => 1,
            "right" => 2,
            "back" or "upright" => 3,
            "up" => 4,
            _ => -1
        };
        if (direction >= 0)
        {
            sprite = actor.Static((FacingDirection)direction);
            return true;
        }
        if (suffix == "downed" && actor.Data.hero)
        {
            sprite = actor.Lying;
            return true;
        }
        return false;
    }
}

public sealed class DQActorClips
{
    public DQActorData Data { get; }
    private readonly SpriteAtlasService _atlas;
    private readonly Dictionary<string, DQClipData> _clipDict = new();
    private readonly Dictionary<string, Sprite[]> _frameDict = new();
    private Sprite[] _statics;
    private Sprite _lying;
    public Sprite Lying => _lying != null ? _lying : _lying = _atlas.Get(Data.lyingKey, Data.atlasPath);

    public DQActorClips(DQActorData data, SpriteAtlasService atlas)
    {
        Data = data;
        _atlas = atlas;
        foreach (var clip in data.clips)
        {
            clip.Validate();
            _clipDict.Add(clip.id, clip);
        }
        if (data.directionSources.Length != 5 || data.staticKeys.Length != 5)
        {
            throw new InvalidOperationException($"DQ directions incomplete: {data.actorId}");
        }
    }

    public DQClipData Clip(string action, FacingDirection direction)
    {
        var key = action == "fall" ? (Data.hero || Data.actorId == "boar" ? "fall_right" : "fall_front")
            : action + "_" + Data.directionSources[(int)direction] + (action == "walk" && Data.hero ? "_24" : "");
        return _clipDict.TryGetValue(key, out var clip) ? clip : null;
    }

    public string PhaseAction(MonsterPhase phase)
    {
        foreach (var binding in Data.phaseBindings)
        {
            if (binding.phase == phase.ToString())
            {
                return binding.action;
            }
        }
        return null;
    }

    public Sprite Static(FacingDirection direction)
    {
        if (_statics == null)
        {
            _statics = new Sprite[5];
            for (var i = 0; i < 5; i++)
            {
                _statics[i] = _atlas.Get(Data.staticKeys[i], Data.atlasPath);
            }
        }
        return _statics[(int)direction];
    }

    public Sprite Frame(DQClipData clip, int frame)
    {
        if (!_frameDict.TryGetValue(clip.id, out var frames))
        {
            frames = new Sprite[clip.frameKeys.Length];
            for (var i = 0; i < frames.Length; i++)
            {
                frames[i] = _atlas.Get(clip.frameKeys[i], Data.atlasPath);
            }
            _frameDict.Add(clip.id, frames);
        }
        return frames[frame];
    }

    public Sprite At(string action, SpriteFacing facing, float seconds, out int frame)
    {
        var clip = Clip(action, facing.Direction);
        if (clip == null)
        {
            throw new InvalidOperationException($"Missing DQ clip {Data.actorId}/{action}/{facing.Label}");
        }
        frame = DQClipMath.FrameAt(clip, seconds);
        return Frame(clip, frame);
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Imperial.Medieval;
using Content.Server.Imperial.Medieval.GameTicking.Rules;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Lycantropy;

public sealed class LycantropyCrimsonTreeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MedievalLycantropyRuleSystem _lycantropyRule = default!;

    private TimeSpan _nextPointsTick = TimeSpan.Zero;
    private readonly List<(EntityUid Uid, MapId MapId, Vector2 Pos)> _lycanCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LycantropyCrimsonTreeComponent, MapInitEvent>(OnTreeMapInit);
        SubscribeLocalEvent<LycantropyCrimsonTreeComponent, ComponentShutdown>(OnTreeShutdown);
        SubscribeLocalEvent<LycantropyCrimsonTreeComponent, StartCollideEvent>(OnTreeStartCollide);
        SubscribeLocalEvent<LycantropyCrimsonTreeComponent, EndCollideEvent>(OnTreeEndCollide);

        SubscribeLocalEvent<LycantropyCompassComponent, MapInitEvent>(OnCompassMapInit);
    }

    private void OnTreeMapInit(EntityUid uid, LycantropyCrimsonTreeComponent comp, MapInitEvent args)
    {
        comp.WitherTime = _timing.CurTime + comp.LifeTime;

        if (TryComp<FixturesComponent>(uid, out var fixtures) &&
            fixtures.Fixtures.TryGetValue(comp.AuraFixtureId, out var fixture) &&
            fixture.Shape is PhysShapeCircle circle)
        {
            _physics.SetRadius(uid, comp.AuraFixtureId, fixture, circle, comp.Radius, fixtures);
        }
    }

    private void OnTreeShutdown(EntityUid uid, LycantropyCrimsonTreeComponent comp, ComponentShutdown args)
    {
        foreach (var target in comp.TrackedEntities)
        {
            RemoveTreeSource(target, uid);
        }

        comp.TrackedEntities.Clear();
    }

    private void OnTreeStartCollide(EntityUid uid, LycantropyCrimsonTreeComponent comp, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != comp.AuraFixtureId)
            return;

        var other = args.OtherEntity;
        if (!IsLycanEntity(other))
            return;

        comp.TrackedEntities.Add(other);
        AddTreeSource(other, uid);
    }

    private void OnTreeEndCollide(EntityUid uid, LycantropyCrimsonTreeComponent comp, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != comp.AuraFixtureId)
            return;

        var other = args.OtherEntity;
        if (!comp.TrackedEntities.Remove(other))
            return;

        RemoveTreeSource(other, uid);
    }

    public void SyncEntityAura(EntityUid uid)
    {
        if (!IsLycanEntity(uid))
            return;

        var entCoords = _transform.GetMapCoordinates(uid);
        var treeQuery = EntityQueryEnumerator<LycantropyCrimsonTreeComponent>();

        while (treeQuery.MoveNext(out var treeUid, out var treeComp))
        {
            var treeCoords = _transform.GetMapCoordinates(treeUid);
            if (treeCoords.MapId != entCoords.MapId)
                continue;

            var distSq = Vector2.DistanceSquared(treeCoords.Position, entCoords.Position);
            if (distSq <= treeComp.Radius * treeComp.Radius)
            {
                treeComp.TrackedEntities.Add(uid);
                AddTreeSource(uid, treeUid);
            }
        }
    }

    private void AddTreeSource(EntityUid target, EntityUid treeUid)
    {
        var hadAura = HasComp<LycantropyCrimsonTreeAuraComponent>(target);
        var aura = EnsureComp<LycantropyCrimsonTreeAuraComponent>(target);
        aura.Sources.Add(treeUid);

        if (!hadAura)
        {
            _popup.PopupEntity(Loc.GetString("popup-lycantropy-crimson-tree-aura-enter"), target, target);
            _lycantropyRule.UpdateAllAlerts();
        }
    }

    private void RemoveTreeSource(EntityUid target, EntityUid treeUid)
    {
        if (TerminatingOrDeleted(target) || !TryComp<LycantropyCrimsonTreeAuraComponent>(target, out var aura))
            return;

        aura.Sources.Remove(treeUid);
        if (aura.Sources.Count > 0)
            return;

        RemComp<LycantropyCrimsonTreeAuraComponent>(target);
        _popup.PopupEntity(Loc.GetString("popup-lycantropy-crimson-tree-aura-leave"), target, target);

        if (HasComp<WerewolfComponent>(target) && !_lycantropyRule.IsNight && !_mobState.IsDead(target))
        {
            _polymorph.Revert(target);
        }

        _lycantropyRule.UpdateAllAlerts();
    }

    private bool IsLycanEntity(EntityUid uid)
    {
        return HasComp<LycantropyComponent>(uid) || HasComp<WerewolfComponent>(uid);
    }

    private void OnCompassMapInit(EntityUid uid, LycantropyCompassComponent comp, MapInitEvent args)
    {
        comp.ExpireTime = _timing.CurTime + comp.Duration;
        comp.NextUpdate = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        UpdateTrees(curTime);
        UpdateAuraPoints(curTime);
        UpdateCompasses(curTime);
    }

    private void UpdateTrees(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<LycantropyCrimsonTreeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime < comp.WitherTime)
                continue;

            var coords = Transform(uid).Coordinates;
            var witheredProto = comp.WitheredPrototype;

            QueueDel(uid);

            if (witheredProto != null)
                Spawn(witheredProto, coords);
        }
    }

    private void UpdateAuraPoints(TimeSpan curTime)
    {
        if (curTime < _nextPointsTick)
            return;

        _nextPointsTick = curTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<LycantropyCrimsonTreeAuraComponent>();
        while (query.MoveNext(out var uid, out var aura))
        {
            if (aura.Sources.Count == 0 || !_mobState.IsAlive(uid))
                continue;

            var rate = 0f;
            foreach (var treeUid in aura.Sources)
            {
                if (TryComp<LycantropyCrimsonTreeComponent>(treeUid, out var tree))
                    rate = Math.Max(rate, tree.PointsPerSecond);
            }

            if (rate <= 0f)
                continue;

            aura.AccumulatedPoints += rate;
            var wholePoints = (int) aura.AccumulatedPoints;
            if (wholePoints <= 0)
                continue;

            aura.AccumulatedPoints -= wholePoints;
            GrantPoints(uid, wholePoints);
        }
    }

    private void GrantPoints(EntityUid uid, int points)
    {
        if (TryComp<LycantropyComponent>(uid, out var lycan))
        {
            lycan.Points += points;
            Dirty(uid, lycan);
            return;
        }

        if (TryComp<PolymorphedEntityComponent>(uid, out var polymorphed) &&
            polymorphed.Parent is { } parent &&
            TryComp<LycantropyComponent>(parent, out var parentLycan))
        {
            parentLycan.Points += points;
            Dirty(parent, parentLycan);
        }
    }

    private void UpdateCompasses(TimeSpan curTime)
    {
        if (Count<LycantropyCompassComponent>() == 0)
            return;

        var cacheBuilt = false;
        var compassQuery = EntityQueryEnumerator<LycantropyCompassComponent>();

        while (compassQuery.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.ExpireTime)
            {
                var coords = Transform(uid).Coordinates;
                var parent = Transform(uid).ParentUid;
                if (parent.IsValid())
                    _popup.PopupEntity(Loc.GetString("popup-lycantropy-compass-expired"), parent, parent);

                QueueDel(uid);
                if (comp.BreakPrototype != null)
                    Spawn(comp.BreakPrototype, coords);

                continue;
            }

            if (curTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = curTime + comp.UpdateInterval;

            if (!cacheBuilt)
            {
                RebuildLycanCache();
                cacheBuilt = true;
            }

            var compassPos = _transform.GetMapCoordinates(uid);
            Vector2? nearest = null;
            var bestDistSq = float.MaxValue;

            foreach (var target in _lycanCache)
            {
                if (target.MapId != compassPos.MapId)
                    continue;

                var distSq = Vector2.DistanceSquared(compassPos.Position, target.Pos);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    nearest = target.Pos;
                }
            }

            if (comp.TargetPosition == nearest)
                continue;

            if (comp.TargetPosition.HasValue && nearest.HasValue &&
                Vector2.DistanceSquared(comp.TargetPosition.Value, nearest.Value) < 0.01f)
            {
                continue;
            }

            comp.TargetPosition = nearest;
            Dirty(uid, comp);
        }
    }

    private void RebuildLycanCache()
    {
        _lycanCache.Clear();

        var humanQuery = EntityQueryEnumerator<LycantropyComponent>();
        while (humanQuery.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            var coords = _transform.GetMapCoordinates(uid);
            _lycanCache.Add((uid, coords.MapId, coords.Position));
        }

        var wolfQuery = EntityQueryEnumerator<WerewolfComponent>();
        while (wolfQuery.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            var coords = _transform.GetMapCoordinates(uid);
            _lycanCache.Add((uid, coords.MapId, coords.Position));
        }
    }
}

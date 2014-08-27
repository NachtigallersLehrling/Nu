﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2014.

namespace Nu
open System
open System.Collections.Generic
open System.ComponentModel
open System.Reflection
open System.Xml
open SDL2
open OpenTK
open Prime
open Nu

// WISDOM: On avoiding threads where possible...
//
// Beyond the cases where persistent threads are absolutely required or where transient threads
// implement embarassingly parallel processes, threads should be AVOIDED as a rule.
//
// If it were the case that physics were processed on a separate hardware component and thereby
// ought to be run on a separate persistent thread, then the proper way to approach the problem of
// physics system queries is to copy the relevant portion of the physics state from the PPU to main
// memory every frame. This way, queries against the physics state can be done IMMEDIATELY with no
// need for complex intermediate states (albeit against a physics state that is one frame old).

// WISDOM: On threading physics...
//
// A simulation that would put physics on another thread should likely do so in a different app
// domain with communication via .NET remoting to make 100% sure that no sharing is happening.

[<AutoOpen>]
module InterativityModule =

    type Interactivity =
        | Gui
        | GuiAndPhysics
        | GuiAndPhysicsAndGamePlay

[<RequireQualifiedAccess>]
module Interactivity =

    let isGamePlaying interactivity =
        match interactivity with
        | Gui -> false
        | GuiAndPhysics -> false
        | GuiAndPhysicsAndGamePlay -> true

    let isPhysicsRunning interactivity =
        match interactivity with
        | Gui -> false
        | GuiAndPhysics -> true
        | GuiAndPhysicsAndGamePlay -> true

[<AutoOpen>]
module TransitionTypeModule =

    type [<StructuralEquality; NoComparison>] TransitionType =
        | Incoming
        | Outgoing

[<AutoOpen>]
module ScreenStateModule =

    type [<StructuralEquality; NoComparison>] ScreenState =
        | IncomingState
        | OutgoingState
        | IdlingState

[<AutoOpen>]
module SimModule =

    type [<CLIMutable; StructuralEquality; NoComparison>] Entity =
        { Id : Guid
          Name : string
          Visible : bool
          OptOverlayName : string option
          Xtension : Xtension }

        static member (?) (this : Entity, memberName) =
            fun args ->
                Xtension.(?) (this.Xtension, memberName) args

        static member (?<-) (this : Entity, memberName, value) =
            let xtension = Xtension.(?<-) (this.Xtension, memberName, value)
            { this with Xtension = xtension }

        static member dispatchesAs dispatcherTargetType entity dispatcherContainer =
            Xtension.dispatchesAs dispatcherTargetType entity.Xtension dispatcherContainer

    type [<CLIMutable; StructuralEquality; NoComparison>] Group =
        { Id : Guid
          Xtension : Xtension }

        static member (?) (this : Group, memberName) =
            fun args ->
                Xtension.(?) (this.Xtension, memberName) args

        static member (?<-) (this : Group, memberName, value) =
            let xtension = Xtension.(?<-) (this.Xtension, memberName, value)
            { this with Xtension = xtension }

        static member dispatchesAs dispatcherTargetType group dispatcherContainer =
            Xtension.dispatchesAs dispatcherTargetType group.Xtension dispatcherContainer

    type [<CLIMutable; StructuralEquality; NoComparison>] Transition =
        { TransitionLifetime : int64
          TransitionTicks : int64
          TransitionType : TransitionType
          OptDissolveImage : Image option }

    type [<CLIMutable; StructuralEquality; NoComparison>] Screen =
        { Id : Guid
          State : ScreenState
          Incoming : Transition
          Outgoing : Transition
          Xtension : Xtension }

        static member (?) (this : Screen, memberName) =
            fun args ->
                Xtension.(?) (this.Xtension, memberName) args

        static member (?<-) (this : Screen, memberName, value) =
            let xtension = Xtension.(?<-) (this.Xtension, memberName, value)
            { this with Xtension = xtension }

        static member dispatchesAs dispatcherTargetType screen dispatcherContainer =
            Xtension.dispatchesAs dispatcherTargetType screen.Xtension dispatcherContainer

    type [<CLIMutable; StructuralEquality; NoComparison>] Game =
        { Id : Guid
          OptSelectedScreenAddress : Address option
          Xtension : Xtension }

        static member (?) (this : Game, memberName) =
            fun args ->
                Xtension.(?) (this.Xtension, memberName) args

        static member (?<-) (this : Game, memberName, value) =
            let xtension = Xtension.(?<-) (this.Xtension, memberName, value)
            { this with Xtension = xtension }

        static member dispatchesAs dispatcherTargetType game dispatcherContainer =
            Xtension.dispatchesAs dispatcherTargetType game.Xtension dispatcherContainer

    type [<StructuralEquality; NoComparison>] Simulant =
        | Game of Game
        | Screen of Screen
        | Group of Group
        | Entity of Entity

    type [<ReferenceEquality>] Task =
        { ScheduledTime : int64
          Operation : World -> World }

    and [<StructuralEquality; NoComparison>] MouseMoveData =
        { Position : Vector2 }

    and [<StructuralEquality; NoComparison>] MouseButtonData =
        { Position : Vector2
          Button : MouseButton }

    and [<StructuralEquality; NoComparison>] EntityCollisionData =
        { Normal : Vector2
          Speed : single
          Collidee : Address }

    and [<StructuralEquality; NoComparison>] EntityChangeData =
        { OldEntity : Entity }

    and [<StructuralEquality; NoComparison>] GroupChangeData =
        { OldGroup : Group }

    and [<StructuralEquality; NoComparison>] ScreenChangeData =
        { OldScreen : Screen }

    and [<StructuralEquality; NoComparison>] OtherData =
        { Obj : obj }

    /// Describes data relevant to specific events.
    and [<ReferenceEquality>] EventData =
        | MouseMoveData of MouseMoveData
        | MouseButtonData of MouseButtonData
        | EntityCollisionData of EntityCollisionData
        | EntityChangeData of EntityChangeData
        | OtherData of OtherData
        | NoData

    /// A generic event for the Nu game engine.
    /// A reference type.
    and [<ReferenceEquality>] Event =
        { Name : Address
          Publisher : Address
          Subscriber : Address
          Data : EventData }

    and EventHandled =
        | Handled
        | Unhandled

    /// Describes a game event subscription.
    /// A reference type.
    and [<ReferenceEquality>] Subscription =
        | ExitSub
        | SwallowSub
        | ScreenTransitionSub of Address (*desinationScreen*)
        | ScreenTransitionFromSplashSub of Address (*desinationScreen*)
        | CustomSub of (Event -> World -> EventHandled * World)

    /// TODO: document
    and SubscriptionEntry = Guid * Address * Subscription

    /// A map of event subscriptions.
    /// A reference type due to the reference-typeness of Subscription.
    /// OPTIMIZATION: due to what seems to be an internal optimization with Map<string, _>, it is
    /// used rather than the more straight-froward Map<Address, _>
    and SubscriptionEntries = Map<string, SubscriptionEntry list>

    /// TODO: document
    and SubscriptionSorter = SubscriptionEntry list -> World -> SubscriptionEntry list

    /// A map of subscription keys to unsubscription data.
    and UnsubscriptionEntries = Map<Guid, Address * Address>

    /// The world, in a functional programming sense.
    and [<ReferenceEquality>] World =
        { Game : Game
          Screens : Map<string, Screen>
          Groups : Map<string, Map<string, Group>>
          Entities : Map<string, Map<string, Map<string, Entity>>>
          EntitiesByAddress : Map<string, Entity>
          TickTime : int64
          Liveness : Liveness
          Interactivity : Interactivity
          Camera : Camera
          Tasks : Task list
          Subscriptions : SubscriptionEntries
          Unsubscriptions : UnsubscriptionEntries
          MouseState : MouseState
          AudioPlayer : AudioPlayer
          Renderer : Renderer
          Integrator : Integrator
          AssetMetadataMap : AssetMetadataMap
          Overlayer : Overlayer
          AudioMessages : AudioMessage rQueue
          RenderMessages : RenderMessage rQueue
          PhysicsMessages : PhysicsMessage rQueue
          Dispatchers : XDispatchers
          ExtData : obj } // TODO: consider if this is still the right approach in the context of the new Xtension stuff

        interface IXDispatcherContainer with
            member this.GetDispatchers () = this.Dispatchers
            end

[<RequireQualifiedAccess>]
module EventData =

    let toMouseMoveData data = match data with MouseMoveData d -> d | _ -> failwith <| "Expected MouseMoveData from event data '" + string data + "'."
    let toMouseButtonData data = match data with MouseButtonData d -> d | _ -> failwith <| "Expected MouseButtonData from event data '" + string data + "'."
    let toEntityCollisionData data = match data with EntityCollisionData d -> d | _ -> failwith <| "Expected EntityCollisionData from event data '" + string data + "'."
    let toOtherData data = match data with OtherData d -> d | _ -> failwith <| "Expected OtherData from event data '" + string data + "'."

[<RequireQualifiedAccess>]
module Sim =

    let getOptChild optChildFinder address parent =
        let optChild = optChildFinder address parent
        match optChild with
        | None -> None
        | Some child -> Some child

    let setOptChild addChild removeChild address parent optChild =
        match optChild with
        | None -> removeChild address parent
        | Some child -> addChild address parent child

    let getChild optChildFinder address parent =
        Option.get <| optChildFinder address parent

    let setChild childAdder childRemover address parent child =
        setOptChild childAdder childRemover address parent (Some child)

    let withSimulant getSimulant setSimulant fn address world : World =
        let simulant = getSimulant address world
        let simulant = fn simulant
        setSimulant address simulant world

    let withSimulantAndWorld getSimulant setSimulant fn address world : World =
        let simulant = getSimulant address world
        let (simulant, world) = fn simulant
        setSimulant address simulant world

    let tryWithSimulant getOptSimulant setSimulant fn address world : World =
        let optSimulant = getOptSimulant address world
        match optSimulant with
        | None -> world
        | Some simulant ->
            let simulant = fn simulant
            setSimulant address simulant world

    let tryWithSimulantAndWorld getOptSimulant setSimulant fn address world : World =
        let optSimulant = getOptSimulant address world
        match optSimulant with
        | None -> world
        | Some simulant ->
            let (simulant, world) = fn simulant
            setSimulant address simulant world

module World =

    let isPhysicsRunning world =
        Interactivity.isPhysicsRunning world.Interactivity

    let isGamePlaying world =
        Interactivity.isGamePlaying world.Interactivity

    let mutable publish = Unchecked.defaultof<SubscriptionSorter -> Address -> Address -> EventData -> World -> World>
    let mutable publish4 = Unchecked.defaultof<Address -> Address -> EventData -> World -> World>
    let mutable subscribe = Unchecked.defaultof<Guid -> Address -> Address -> Subscription -> World -> World>
    let mutable subscribe4 = Unchecked.defaultof<Address -> Address -> Subscription -> World -> World>
    let mutable unsubscribe = Unchecked.defaultof<Guid -> World -> World>
    let mutable withSubscription = Unchecked.defaultof<Address -> Address -> Subscription -> (World -> World) -> World -> World>
    let mutable observe = Unchecked.defaultof<Address -> Address -> Subscription -> World -> World>

[<AutoOpen>]
module WorldPhysicsModule =

    type World with

        static member createBody entityAddress physicsId position rotation bodyProperties world =
            let createBodyMessage = CreateBodyMessage { EntityAddress = entityAddress; PhysicsId = physicsId; Position = position; Rotation = rotation; BodyProperties = bodyProperties }
            { world with PhysicsMessages = createBodyMessage :: world.PhysicsMessages }

        static member destroyBody physicsId world =
            let destroyBodyMessage = DestroyBodyMessage { PhysicsId = physicsId }
            { world with PhysicsMessages = destroyBodyMessage :: world.PhysicsMessages }

        static member setPosition position physicsId world =
            let setPositionMessage = SetPositionMessage { PhysicsId = physicsId; Position = position }
            { world with PhysicsMessages = setPositionMessage :: world.PhysicsMessages }

        static member setRotation rotation physicsId world =
            let setRotationMessage = SetRotationMessage { PhysicsId = physicsId; Rotation = rotation }
            { world with PhysicsMessages = setRotationMessage :: world.PhysicsMessages }

        static member setLinearVelocity linearVelocity physicsId world =
            let setLinearVelocityMessage = SetLinearVelocityMessage { PhysicsId = physicsId; LinearVelocity = linearVelocity }
            { world with PhysicsMessages = setLinearVelocityMessage :: world.PhysicsMessages }

        static member applyLinearImpulse linearImpulse physicsId world =
            let applyLinearImpulseMessage = ApplyLinearImpulseMessage { PhysicsId = physicsId; LinearImpulse = linearImpulse }
            { world with PhysicsMessages = applyLinearImpulseMessage :: world.PhysicsMessages }

        static member applyForce force physicsId world =
            let applyForceMessage = ApplyForceMessage { PhysicsId = physicsId; Force = force }
            { world with PhysicsMessages = applyForceMessage :: world.PhysicsMessages }

[<AutoOpen>]
module WorldRenderingModule =

    type World with

        static member hintRenderingPackageUse fileName packageName world =
            let hintRenderingPackageUseMessage = HintRenderingPackageUseMessage { FileName = fileName; PackageName = packageName }
            { world with RenderMessages = hintRenderingPackageUseMessage :: world.RenderMessages }

        static member hintRenderingPackageDisuse fileName packageName world =
            let hintRenderingPackageDisuseMessage = HintRenderingPackageDisuseMessage { FileName = fileName; PackageName = packageName }
            { world with RenderMessages = hintRenderingPackageDisuseMessage :: world.RenderMessages }

        static member reloadRenderingAssets fileName world =
            let reloadRenderingAssetsMessage = ReloadRenderingAssetsMessage { FileName = fileName }
            { world with RenderMessages = reloadRenderingAssetsMessage :: world.RenderMessages }

[<AutoOpen>]
module WorldAudioModule =

    type World with

        static member playSong song volume timeToFadeOutSongMs world =
            let playSongMessage = PlaySongMessage { Song = song; Volume = volume; TimeToFadeOutSongMs = timeToFadeOutSongMs }
            { world with AudioMessages = playSongMessage :: world.AudioMessages }

        static member playSong6 songAssetName packageName packageFileName volume timeToFadeOutSongMs world =
            let song = { SongAssetName = songAssetName; PackageName = packageName; PackageFileName = packageFileName }
            World.playSong song volume timeToFadeOutSongMs world

        static member playSound sound volume world =
            let playSoundMessage = PlaySoundMessage { Sound = sound; Volume = volume }
            { world with AudioMessages = playSoundMessage :: world.AudioMessages }

        static member playSound5 soundAssetName packageName packageFileName volume world =
            let sound = { SoundAssetName = soundAssetName; PackageName = packageName; PackageFileName = packageFileName }
            World.playSound sound volume world

        static member fadeOutSong timeToFadeOutSongMs world =
            let fadeOutSongMessage = FadeOutSongMessage timeToFadeOutSongMs
            { world with AudioMessages = fadeOutSongMessage :: world.AudioMessages }

        static member stopSong world =
            { world with AudioMessages = StopSongMessage :: world.AudioMessages }

        static member hintAudioPackageUse fileName packageName world =
            let hintAudioPackageUseMessage = HintAudioPackageUseMessage { FileName = fileName; PackageName = packageName }
            { world with AudioMessages = hintAudioPackageUseMessage :: world.AudioMessages }

        static member hintAudioPackageDisuse fileName packageName world =
            let hintAudioPackageDisuseMessage = HintAudioPackageDisuseMessage { FileName = fileName; PackageName = packageName }
            { world with AudioMessages = hintAudioPackageDisuseMessage :: world.AudioMessages }

        static member reloadAudioAssets fileName world =
            let reloadAudioAssetsMessage = ReloadAudioAssetsMessage { FileName = fileName }
            { world with AudioMessages = reloadAudioAssetsMessage :: world.AudioMessages }
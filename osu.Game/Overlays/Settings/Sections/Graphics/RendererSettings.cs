// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    public partial class RendererSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GraphicsSettingsStrings.RendererHeader;

        private bool automaticRendererInUse;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        private partial class RendererChangeNotification : SimpleNotification
        {
            public override bool IsImportant => true;
            private OsuGame? game;

            public RendererChangeNotification(OsuGame? game)
            {
                Text = GraphicsSettingsStrings.SettingRequiresRestart;
                this.game = game;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, INotificationOverlay notificationOverlay)
            {
                Icon = FontAwesome.Solid.InfoCircle;
                IconContent.Colour = colours.Yellow;

                Activated = delegate
                {
                    notificationOverlay.Hide();
                    game?.AttemptExit();
                    return true;
                };
            }
        }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config, OsuConfigManager osuConfig, IDialogOverlay? dialogOverlay, OsuGame? game, GameHost host)
        {
            var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
            automaticRendererInUse = renderer.Value == RendererType.Automatic;

            SettingsEnumDropdown<RendererType> rendererDropdown;

            Children = new Drawable[]
            {
                rendererDropdown = new RendererSettingsDropdown
                {
                    LabelText = GraphicsSettingsStrings.Renderer,
                    Current = renderer,
                    Items = host.GetPreferredRenderersForCurrentPlatform().OrderBy(t => t).Where(t => t != RendererType.Vulkan),
                    Keywords = new[] { @"compatibility", @"directx" },
                },
                // TODO: this needs to be a custom dropdown at some point
                new SettingsEnumDropdown<FrameSync>
                {
                    LabelText = GraphicsSettingsStrings.FrameLimiter,
                    Current = config.GetBindable<FrameSync>(FrameworkSetting.FrameSync),
                    Keywords = new[] { @"fps" },
                },
                new SettingsEnumDropdown<ExecutionMode>
                {
                    LabelText = GraphicsSettingsStrings.ThreadingMode,
                    Current = config.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode)
                },
                new SettingsCheckbox
                {
                    LabelText = GraphicsSettingsStrings.ShowFPS,
                    Current = osuConfig.GetBindable<bool>(OsuSetting.ShowFpsDisplay)
                },
            };

            renderer.BindValueChanged(r =>
            {
                if (r.NewValue == host.ResolvedRenderer)
                    return;

                // Need to check startup renderer for the "automatic" case, as ResolvedRenderer above will track the final resolved renderer instead.
                if (r.NewValue == RendererType.Automatic && automaticRendererInUse)
                    return;

                notificationOverlay?.Post(new RendererChangeNotification(game));
            });

            // TODO: remove this once we support SDL+android.
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Android)
            {
                rendererDropdown.Items = new[] { RendererType.Automatic, RendererType.OpenGLLegacy };
                rendererDropdown.SetNoticeText("New renderer support for android is coming soon!", true);
            }
        }

        private partial class RendererSettingsDropdown : SettingsEnumDropdown<RendererType>
        {
            protected override OsuDropdown<RendererType> CreateDropdown() => new RendererDropdown();

            protected partial class RendererDropdown : DropdownControl
            {
                private RendererType hostResolvedRenderer;
                private bool automaticRendererInUse;

                [BackgroundDependencyLoader]
                private void load(FrameworkConfigManager config, GameHost host)
                {
                    var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
                    automaticRendererInUse = renderer.Value == RendererType.Automatic;
                    hostResolvedRenderer = host.ResolvedRenderer;
                }

                protected override LocalisableString GenerateItemText(RendererType item)
                {
                    if (item == RendererType.Automatic && automaticRendererInUse)
                        return LocalisableString.Interpolate($"{base.GenerateItemText(item)} ({hostResolvedRenderer.GetDescription()})");

                    return base.GenerateItemText(item);
                }
            }
        }
    }
}

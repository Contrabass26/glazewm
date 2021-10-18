﻿using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Windows.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Utils;

namespace GlazeWM.Domain.Windows.CommandHandlers
{
  class ToggleFloatingHandler : ICommandHandler<ToggleFloatingCommand>
  {
    private Bus _bus;
    private WorkspaceService _workspaceService;
    private WindowService _windowService;
    private ContainerService _containerService;

    public ToggleFloatingHandler(Bus bus, WorkspaceService workspaceService, WindowService windowService, ContainerService containerService)
    {
      _bus = bus;
      _workspaceService = workspaceService;
      _windowService = windowService;
      _containerService = containerService;
    }

    public CommandResponse Handle(ToggleFloatingCommand command)
    {
      var window = command.Window;

      if (window is FloatingWindow)
        DisableFloating(window as FloatingWindow);

      else
        EnableFloating(window);

      return CommandResponse.Ok;
    }

    private void EnableFloating(Window window)
    {
      // Keep reference to the window's ancestor workspace and focus order index prior to detaching.
      var workspace = _workspaceService.GetWorkspaceFromChildContainer(window);
      var focusOrderIndex = window.Parent.ChildFocusOrder.IndexOf(window);

      // Create a floating window and place it in the center of the workspace.
      var floatingWindow = new FloatingWindow(
        window.Hwnd,
        window.OriginalWidth,
        window.OriginalHeight,
        workspace.X + (workspace.Width / 2) - (window.OriginalWidth / 2),
        workspace.Y + (workspace.Height / 2) - (window.OriginalHeight / 2)
      );

      _bus.Invoke(new DetachContainerCommand(window));
      _bus.Invoke(new AttachContainerCommand(workspace, floatingWindow));

      if (focusOrderIndex != -1)
        floatingWindow.Parent.ChildFocusOrder.Insert(focusOrderIndex, floatingWindow);

      _bus.Invoke(new RedrawContainersCommand());
    }

    private void DisableFloating(FloatingWindow floatingWindow)
    {
      // Keep reference to the window's ancestor workspace prior to detaching.
      var workspace = _workspaceService.GetWorkspaceFromChildContainer(floatingWindow);

      // Get the original width and height of the window.
      var originalPlacement = _windowService.GetPlacementOfHandle(floatingWindow.Hwnd).NormalPosition;
      var originalWidth = originalPlacement.Right - originalPlacement.Left;
      var originalHeight = originalPlacement.Bottom - originalPlacement.Top;

      var insertionTarget = workspace.LastFocusedDescendantOfType(typeof(IResizable));

      var tilingWindow = new TilingWindow(floatingWindow.Hwnd, originalWidth, originalHeight);
      _bus.Invoke(new ReplaceContainerCommand(floatingWindow.Parent, floatingWindow.Index, tilingWindow));

      // Descend the tree of the current workspace and insert the created tiling window.
      if (insertionTarget == null)
        _bus.Invoke(new AttachContainerCommand(workspace, tilingWindow));
      else
      {
        MoveWithinTree(tilingWindow, insertionTarget, insertionTarget.Index + 1);
      }

      _bus.Invoke(new RedrawContainersCommand());
    }

    private void MoveWithinTree(Container container, Container target, int index)
    {
      // Get lowest common ancestor (LCA) between `container` and `target`.
      var lowestCommonAncestor = _containerService.GetLowestCommonAncestor(container, target);

      // Get ancestors of `container` and `target` that are direct children of LCA.
      var containerAncestor = GetChildWithAncestor(lowestCommonAncestor, container);
      var targetAncestor = GetChildWithAncestor(lowestCommonAncestor, target);

      var containerAncestorFocusIndex = lowestCommonAncestor.ChildFocusOrder.IndexOf(containerAncestor);
      var targetAncestorFocusIndex = lowestCommonAncestor.ChildFocusOrder.IndexOf(targetAncestor);

      // Get whether the ancestor of `container` appears before `target`'s ancestor in the
      // `ChildFocusOrder` of LCA. If it does, then target's ancestor should be placed before
      // the original ancestor in LCA's `ChildFocusOrder`.
      var isFocusedDescendant = containerAncestor.LastFocusedDescendant == container;
      var shouldFocusBefore = containerAncestor == targetAncestor ? isFocusedDescendant
       : containerAncestorFocusIndex < targetAncestorFocusIndex;

      if (isFocusedDescendant && shouldFocusBefore)
        lowestCommonAncestor.ChildFocusOrder.ShiftToIndex(
          containerAncestorFocusIndex,
          targetAncestor
        );

      _bus.Invoke(new DetachContainerCommand(container));
      _bus.Invoke(new AttachContainerCommand(target.Parent as SplitContainer, container, index));

      if (target.Parent == lowestCommonAncestor)
      {
        // TODO: Add `FocusIndex` getter.
        var focusIndex = shouldFocusBefore ?
          lowestCommonAncestor.ChildFocusOrder.IndexOf(target) : lowestCommonAncestor.ChildFocusOrder.IndexOf(target) + 1;

        lowestCommonAncestor.ChildFocusOrder.ShiftToIndex(focusIndex, container);
        return;
      }

      if (shouldFocusBefore)
        target.Parent.ChildFocusOrder.MoveToFront(container);
    }

    private Container GetChildWithAncestor(Container ancestor, Container container)
    {
      var child = container;

      while (child != null && child.Parent != ancestor)
        child = child.Parent;

      return child;
    }
  }
}

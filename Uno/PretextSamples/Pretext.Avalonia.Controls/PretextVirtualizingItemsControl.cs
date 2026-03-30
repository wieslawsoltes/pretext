using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Pretext.Avalonia.Controls;

public class PretextVirtualizingItemsControl : ItemsControl, ILogicalScrollable
{
    private ILogicalScrollable? _presenterScrollable;
    private EventHandler? _scrollInvalidated;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll;
    private Vector _offset;
    private Size _extent;
    private Size _viewport;
    private Size _scrollSize = new(50, 50);
    private Size _pageScrollSize;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get
        {
            EnsurePresenterBridge();
            return _presenterScrollable?.CanHorizontallyScroll ?? _canHorizontallyScroll;
        }
        set
        {
            _canHorizontallyScroll = value;
            EnsurePresenterBridge();
            if (_presenterScrollable is not null)
            {
                _presenterScrollable.CanHorizontallyScroll = value;
            }
        }
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get
        {
            EnsurePresenterBridge();
            return _presenterScrollable?.CanVerticallyScroll ?? _canVerticallyScroll;
        }
        set
        {
            _canVerticallyScroll = value;
            EnsurePresenterBridge();
            if (_presenterScrollable is not null)
            {
                _presenterScrollable.CanVerticallyScroll = value;
            }
        }
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize
    {
        get
        {
            EnsurePresenterBridge();
            SyncFromPresenter();
            return _scrollSize;
        }
    }

    Size ILogicalScrollable.PageScrollSize
    {
        get
        {
            EnsurePresenterBridge();
            SyncFromPresenter();
            return _pageScrollSize;
        }
    }

    Size IScrollable.Extent
    {
        get
        {
            EnsurePresenterBridge();
            SyncFromPresenter();
            return _extent;
        }
    }

    Vector IScrollable.Offset
    {
        get
        {
            EnsurePresenterBridge();
            SyncFromPresenter();
            return _offset;
        }
        set
        {
            _offset = value;
            EnsurePresenterBridge();
            if (_presenterScrollable is not null)
            {
                _presenterScrollable.Offset = value;
                SyncFromPresenter();
            }

            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    Size IScrollable.Viewport
    {
        get
        {
            EnsurePresenterBridge();
            SyncFromPresenter();
            return _viewport;
        }
    }

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
    {
        EnsurePresenterBridge();
        return _presenterScrollable?.BringIntoView(target, targetRect) ?? false;
    }

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from)
    {
        EnsurePresenterBridge();
        return _presenterScrollable?.GetControlInDirection(direction, from);
    }

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
    {
        _scrollInvalidated?.Invoke(this, e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        EnsurePresenterBridge(force: true);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePresenterBridge();
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsurePresenterBridge();
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_presenterScrollable is not null)
        {
            _presenterScrollable.ScrollInvalidated -= OnPresenterScrollInvalidated;
            _presenterScrollable = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void EnsurePresenterBridge(bool force = false)
    {
        var presenterScrollable = Presenter as ILogicalScrollable;
        if (!force && ReferenceEquals(presenterScrollable, _presenterScrollable))
        {
            return;
        }

        if (_presenterScrollable is not null)
        {
            _presenterScrollable.ScrollInvalidated -= OnPresenterScrollInvalidated;
        }

        _presenterScrollable = presenterScrollable;
        if (_presenterScrollable is not null)
        {
            _presenterScrollable.CanHorizontallyScroll = _canHorizontallyScroll;
            _presenterScrollable.CanVerticallyScroll = _canVerticallyScroll;
            _presenterScrollable.Offset = _offset;
            _presenterScrollable.ScrollInvalidated += OnPresenterScrollInvalidated;
            SyncFromPresenter();
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPresenterScrollInvalidated(object? sender, EventArgs e)
    {
        SyncFromPresenter();
        _scrollInvalidated?.Invoke(this, e);
    }

    private void SyncFromPresenter()
    {
        if (_presenterScrollable is null)
        {
            return;
        }

        _extent = _presenterScrollable.Extent;
        _viewport = _presenterScrollable.Viewport;
        _offset = _presenterScrollable.Offset;
        _scrollSize = _presenterScrollable.ScrollSize;
        _pageScrollSize = _presenterScrollable.PageScrollSize;
    }
}

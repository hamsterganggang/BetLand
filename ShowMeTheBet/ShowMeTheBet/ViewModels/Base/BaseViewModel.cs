using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShowMeTheBet.ViewModels.Base;

/// <summary>
/// MVVM 패턴을 위한 기본 ViewModel.
/// - INotifyPropertyChanged 구현
/// - 상태 변경 시 컴포넌트가 다시 렌더링될 수 있도록 알림 제공
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Blazor 컴포넌트에 상태 변경을 알려주기 위한 이벤트
    /// </summary>
    public event Action? StateChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 상태 변경 알림 메서드
    /// </summary>
    /// <param name="propertyName">변경된 속성 이름</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        StateChanged?.Invoke();
    }

    /// <summary>
    /// 속성 변경을 캡슐화한 헬퍼 메서드
    /// </summary>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public virtual void Dispose()
    {
        StateChanged = null;
        PropertyChanged = null;
    }
}


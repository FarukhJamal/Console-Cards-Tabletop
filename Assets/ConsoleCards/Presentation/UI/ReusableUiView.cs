using System;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Common lifecycle for UI instances that are cached or returned to a pool.
    /// Feature-specific Bind methods remain on the concrete View.
    /// </summary>
    public abstract class ReusableUiView : MonoBehaviour
    {
        private bool isAcquired;

        public bool IsAcquired => isAcquired;

        public void Acquire()
        {
            if (isAcquired)
            {
                throw new InvalidOperationException($"{GetType().Name} is already acquired.");
            }

            isAcquired = true;
            gameObject.SetActive(false);
            OnAcquire();
        }

        public virtual void Show()
        {
            RequireAcquired();
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }

        public abstract void Unbind();

        public void Release()
        {
            if (!isAcquired)
            {
                return;
            }

            Hide();
            Unbind();
            OnRelease();
            isAcquired = false;
        }

        protected virtual void OnAcquire()
        {
        }

        protected virtual void OnRelease()
        {
        }

        protected void RequireAcquired()
        {
            if (!isAcquired)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} must be acquired before it is bound or shown.");
            }
        }

        protected virtual void OnDestroy()
        {
            Release();
        }
    }
}

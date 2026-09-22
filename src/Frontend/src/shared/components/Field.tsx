import { Children, cloneElement, isValidElement, useId, type ReactElement, type ReactNode } from 'react';

/** Label + champ de formulaire + message d'erreur react-hook-form, style standard. */
export function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: ReactNode;
}) {
  const generatedId = useId();
  const errorId = `${generatedId}-error`;

  const control = Children.map(children, (child) => {
    if (!isValidElement(child)) return child;
    const el = child as ReactElement<{ id?: string; 'aria-invalid'?: boolean; 'aria-describedby'?: string }>;
    return cloneElement(el, {
      id: el.props.id ?? generatedId,
      'aria-invalid': error ? true : undefined,
      'aria-describedby': error ? errorId : undefined,
    });
  });

  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={generatedId} className="font-medium text-slate-700">
        {label}
      </label>
      {control}
      {error && (
        <span id={errorId} role="alert" className="text-xs text-rose-600">
          {error}
        </span>
      )}
    </div>
  );
}

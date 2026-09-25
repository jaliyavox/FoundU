import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

export interface SelectOption {
  value: string
  label: string
}

/**
 * Thin wrapper over the Base UI select so its API lives in one place. Base UI has no
 * `placeholder` prop - the value renders through a child function instead, which is what
 * lets us show placeholder text when nothing is chosen yet.
 */
export function FormSelect({
  id,
  value,
  onValueChange,
  options,
  placeholder,
  disabled,
  invalid,
}: {
  id: string
  value: string
  onValueChange: (value: string) => void
  options: SelectOption[]
  placeholder: string
  disabled?: boolean
  invalid?: boolean
}) {
  return (
    <Select value={value || null} onValueChange={(next) => onValueChange((next as string) ?? '')}>
      <SelectTrigger id={id} disabled={disabled} aria-invalid={invalid} className="w-full">
        <SelectValue>
          {(selected) => {
            const match = options.find((option) => option.value === selected)
            return match ? (
              match.label
            ) : (
              <span className="text-muted-foreground">{placeholder}</span>
            )
          }}
        </SelectValue>
      </SelectTrigger>

      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

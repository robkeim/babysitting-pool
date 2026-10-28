import { Dialog as DialogPrimitive } from "@base-ui/react/dialog"
import { cn } from "cn"

function Sheet(props: DialogPrimitive.Root.Props) {
  return <DialogPrimitive.Root data-slot="sheet" {...props} />
}

const SheetTrigger = DialogPrimitive.Trigger

/** A panel that slides up from the bottom of the screen over a dimmed page. */
function SheetContent({ className, ...props }: DialogPrimitive.Popup.Props) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Backdrop
        data-slot="sheet-backdrop"
        className="fixed inset-0 z-40 bg-black/50 transition-opacity data-ending-style:opacity-0 data-starting-style:opacity-0"
      />
      <DialogPrimitive.Popup
        data-slot="sheet-content"
        className={cn(
          "fixed inset-x-0 bottom-0 z-50 flex flex-col gap-1 rounded-t-xl bg-popover p-4 pb-[calc(1rem+env(safe-area-inset-bottom))] text-sm text-popover-foreground ring-1 ring-foreground/10 transition-transform data-ending-style:translate-y-full data-starting-style:translate-y-full",
          className
        )}
        {...props}
      />
    </DialogPrimitive.Portal>
  )
}

function SheetTitle({ className, ...props }: DialogPrimitive.Title.Props) {
  return (
    <DialogPrimitive.Title
      data-slot="sheet-title"
      className={cn("px-2 pb-1 text-sm font-medium text-muted-foreground", className)}
      {...props}
    />
  )
}

export { Sheet, SheetContent, SheetTitle, SheetTrigger }

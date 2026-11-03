Use the coding standards for WPF, comment code and make it readable.
Keep the documentation of project up-to-date. Document everything you do in docs/ folder.
Write unit tests for the functionalities you add.

## Csharp Coding standards
- make sure every block statment (eg. for, if, while, foreach etc.) have curly brackets
- make sure there is blank line around all block statements
- private fields in class start with lowercase letter
- skip EOF newline warnings; C#/.NET on Windows, user considers it noise
- Order by Member Type first, then by Accessor
1. Fields
2. Constants
3. Properties
4. Indexers
5. Constructors
6. Destructors / Finalizers
7. Methods
8. Operators
9. Events
10. Nested Types (enums, classes, structs)
Within each group, order by access modifier:
1. public
2. internal
3. protected internal
4. protected
5. private protected
6. private
- prefer to have explicit compare for boolean values with true/false values for readability, eg. `isNew == true`
- start enum values from 1, never 0, so a default (unset) value is never mistaken for a valid one
- one class, one file
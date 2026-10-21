# Application use cases are plain handler classes, not a mediator

Each command and query is its own handler class that Api endpoints call directly, with ports defined in Application. We rejected MediatR-style mediators: the library has moved to a commercial license and the indirection buys a toy project nothing. CQRS remains a principle: commands go through domain objects, queries may project straight from the database.

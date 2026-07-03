# Attendance Employee Shift Rules - v4.11.25

## Business rules implemented

- Default store/male timing: 09:00 to 21:00.
- Default store shift has 1.5 hour lunch break: 13:00 to 14:30.
- Default store shift requires Break Out and Break In punches.
- Session based attendance is used for retail shifts.
- One completed session counts as half-day.
- Two completed sessions count as full day.
- Female default rule: 10:00 to 20:00 no-break shift.
- Accounts and housekeeping shifts are seeded but should be assigned by Employee Shift Rules where needed.
- Housekeeping morning/evening shifts exist so each housekeeping employee can be assigned separately.

## Priority

1. Employee-specific rule
2. Category rule
3. Department rule
4. Designation rule
5. Gender rule
6. Store default rule

Lower priority number wins.

## Pages

- Attendance > Shifts
- Attendance > Employee Shift Rules

## Punch sequence

For break shift:

1. CheckIn
2. BreakOut
3. BreakIn
4. CheckOut

For no-break shift:

1. CheckIn
2. CheckOut

## Daily Attendance sync

AttendancePunches remain the source of exact CheckIn, BreakOut, BreakIn, and CheckOut times.
The legacy HR Attendance table is updated with CheckInTime, CheckOutTime, and Present/HalfDay/Absent status.

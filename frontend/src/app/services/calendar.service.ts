import { Response, URLSearchParams } from '@angular/http';
import { CustomHttp } from '../core/custom-http';
import { Observable } from 'rxjs';
import { Injectable, EventEmitter } from '@angular/core';
import { TimeEntry, CalendarDay, DateUtils } from '../models/calendar';
import { ArrayUtils } from '../core/object-utils';
import { ConstantService } from '../core/constant.service';
import * as moment from 'moment';

@Injectable()
export class CalendarService {
	dragEffect: string = 'move';
	draggedTimeEntry: TimeEntry;
	fakeCalendarTaskHeight: number;
	firstDayOfWeek: number;
	isAltPressed: boolean = false;
	isTimerActivated: boolean;
	isTimeEntryFormOpened: boolean = false;
	timeEntriesUpdated: EventEmitter<void> = new EventEmitter<void>();

	calendar: CalendarDay[] = [];

	constructor(private constantService: ConstantService,
	            private http: CustomHttp) {
	}

	getTimeEntries(dateFrom: string, dif?: number): Observable<TimeEntry[]> {
		let dateTo = moment(this.moveDate(dateFrom, dif || 1)).toDate();
		let newDateTo = DateUtils.formatDateToString(dateTo.setDate(dateTo.getDate() - 1));

		let params = new URLSearchParams();
		params.set('dateBegin', dateFrom + 'T00:00:00Z');
		params.set('dateEnd', newDateTo + 'T23:59:59Z');

		return this.http.get(this.constantService.timeEntriesApi, {search: params})
			.map((res: Response) => {
				let timeEntries = this.sortTimeEntries(res.json());
				return timeEntries.map((x: any) => new TimeEntry(x));
			});
	}

	Delete(id: string): Observable<Response> {
		return this.http.delete(this.constantService.timeEntriesApi + id);
	}

	Post(obj: TimeEntry): Observable<any> {
		return this.http.post(this.constantService.timeEntriesApi, obj)
			.map((res: Response) => res.json());
	}

	Put(obj: TimeEntry, id: string): Observable<TimeEntry[]> {
		return this.http.put(this.constantService.timeEntriesApi + id, obj)
			.map((res: Response) => res.json());
	}

	getDayInfoByDate(timeEntryDate: string): CalendarDay {
		return this.calendar.find((day: CalendarDay) => {
			return moment(day.date).toDate().getDate() === moment(timeEntryDate).toDate().getDate();
		});
	}

	getTotalTimeForDay(day: CalendarDay, timeField: string): number {
		let totalTime: number = 0;
		day && day.timeEntries.forEach((timeEntry: TimeEntry) => {
			totalTime += timeEntry[timeField];
		});

		return totalTime;
	}

	getWeekBeginning(date: string, firstDayOfWeek: number): string {
		let thisDate = moment(date).toDate();
		let firstDayCorrection = (thisDate.getDay() < firstDayOfWeek) ? -7 : 0;
		let dayCorrection = thisDate.setDate(thisDate.getDate() - thisDate.getDay() + firstDayOfWeek + firstDayCorrection);
		return DateUtils.formatDateToString(new Date(dayCorrection));
	}

	private sortTimeEntries(timeEntries: TimeEntry[]): TimeEntry[] {
		let arrayWithFromToPeriod = timeEntries.filter((timeEntry) => timeEntry.isFromToShow === true);
		let otherTimeEntries = timeEntries.filter((timeEntry) => timeEntry.isFromToShow === false);

		ArrayUtils.sortByField(arrayWithFromToPeriod, 'timeFrom');
		ArrayUtils.sortByField(otherTimeEntries, 'id');

		return [...arrayWithFromToPeriod, ...otherTimeEntries];
	}

	private moveDate(date: string, dif: number): string {
		let newDate = moment(date).toDate();
		return DateUtils.formatDateToString(newDate.setDate(newDate.getDate() + dif));
	}
}

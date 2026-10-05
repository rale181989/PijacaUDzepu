import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SetupAccountPage } from './setup-account.page';

describe('SetupAccountPage', () => {
  let component: SetupAccountPage;
  let fixture: ComponentFixture<SetupAccountPage>;

  beforeEach(() => {
    fixture = TestBed.createComponent(SetupAccountPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
